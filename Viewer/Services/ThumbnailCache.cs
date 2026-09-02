using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Viewer.Loaders;

namespace Viewer.Services;

public sealed class ThumbnailCache
{
    private const int ThumbSize = 100;
    private readonly ImageLoaderRegistry _registry;
    private readonly ConcurrentDictionary<string, BitmapSource?> _memCache = new(StringComparer.OrdinalIgnoreCase);

    // Persisted across sessions - re-opening a folder doesn't have to re-decode
    // every file (PSD/HEIC/SAI2 thumbnails in particular aren't cheap) just to
    // rebuild the filmstrip.
    private static readonly string DiskCacheDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BananaView", "ThumbnailCache");

    public ThumbnailCache(ImageLoaderRegistry registry) => _registry = registry;

    public async Task<BitmapSource?> GetThumbnailAsync(string path)
    {
        if (_memCache.TryGetValue(path, out var cached)) return cached;

        BitmapSource? thumb;
        try
        {
            thumb = await Task.Run(() => LoadFromDiskOrCreate(path));
        }
        catch
        {
            thumb = null;
        }

        _memCache[path] = thumb;
        return thumb;
    }

    private BitmapSource? LoadFromDiskOrCreate(string path)
    {
        var diskPath = GetDiskCachePath(path);

        if (diskPath != null && File.Exists(diskPath))
        {
            try
            {
                var cachedBmp = new BitmapImage();
                cachedBmp.BeginInit();
                cachedBmp.CacheOption = BitmapCacheOption.OnLoad;
                cachedBmp.UriSource = new Uri(diskPath, UriKind.Absolute);
                cachedBmp.EndInit();
                cachedBmp.Freeze();
                return cachedBmp;
            }
            catch
            {
                // Fall through and regenerate if the cached file is somehow corrupt.
            }
        }

        var thumb = CreateThumbnail(path);
        if (thumb != null && diskPath != null)
        {
            try
            {
                Directory.CreateDirectory(DiskCacheDir);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(thumb));
                using var fs = File.Create(diskPath);
                encoder.Save(fs);
            }
            catch
            {
                // Disk cache is a nice-to-have, not required for the thumbnail itself.
            }
        }
        return thumb;
    }

    // Keyed by path + last-write-time + length, so an edited/replaced file
    // naturally invalidates (gets a different key) instead of showing a stale thumbnail.
    private static string? GetDiskCachePath(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return null;
            var key = $"{info.FullName.ToLowerInvariant()}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            return Path.Combine(DiskCacheDir, hash + ".png");
        }
        catch
        {
            return null;
        }
    }

    private BitmapSource? CreateThumbnail(string path)
    {
        var ext = Path.GetExtension(path);

        if (string.Equals(ext, ".gif", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(path);
            var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return Downscale(decoder.Frames[0]);
        }

        if (string.Equals(ext, ".procreate", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            // Prefer the small QuickLook thumbnail over the full-size preview here - it's plenty for a filmstrip tile and much faster to decode.
            var entry =
                archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, "QuickLook/Thumbnail.png", StringComparison.OrdinalIgnoreCase)) ??
                archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, "QuickLook/Preview.png", StringComparison.OrdinalIgnoreCase)) ??
                archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("QuickLook/", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            if (entry == null) return null;

            using var entryStream = entry.Open();
            var decoder = new PngBitmapDecoder(entryStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return Downscale(decoder.Frames[0]);
        }

        if (string.Equals(ext, ".sai2", StringComparison.OrdinalIgnoreCase))
        {
            var decoded = Sai2NativeDecoder.DecodeFlattened(path);
            var sai2Bmp = BitmapSource.Create(
                decoded.Width, decoded.Height, 96, 96,
                PixelFormats.Bgra32, null,
                decoded.Bgra, decoded.Width * 4);
            return Downscale(sai2Bmp);
        }

        if (string.Equals(ext, ".icns", StringComparison.OrdinalIgnoreCase))
        {
            var png = IcnsImageLoader.FindLargestPngChunk(File.ReadAllBytes(path));
            if (png == null) return null;
            var iBmp = new BitmapImage();
            using (var ms = new MemoryStream(png))
            {
                iBmp.BeginInit();
                iBmp.CacheOption = BitmapCacheOption.OnLoad;
                iBmp.StreamSource = ms;
                iBmp.EndInit();
            }
            return Downscale(iBmp);
        }

        if (_registry.GetLoader(ext) is MagickImageLoader)
        {
            byte[] pngBytes;
            if (string.Equals(ext, ".ico", StringComparison.OrdinalIgnoreCase))
            {
                using var collection = new MagickImageCollection(path);
                var best = collection.OrderByDescending(i => i.Width).First();
                best.Thumbnail(new MagickGeometry(ThumbSize, ThumbSize));
                best.Format = MagickFormat.Png32;
                pngBytes = best.ToByteArray();
            }
            else
            {
                using var image = new MagickImage(path);
                image.Thumbnail(new MagickGeometry(ThumbSize, ThumbSize));
                image.Format = MagickFormat.Png32;
                pngBytes = image.ToByteArray();
            }

            var mBmp = new BitmapImage();
            using (var ms = new MemoryStream(pngBytes))
            {
                mBmp.BeginInit();
                mBmp.CacheOption = BitmapCacheOption.OnLoad;
                mBmp.StreamSource = ms;
                mBmp.EndInit();
            }
            mBmp.Freeze();
            return mBmp;
        }

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.DecodePixelWidth = ThumbSize;
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private static BitmapSource Downscale(BitmapSource src)
    {
        var maxDim = Math.Max(src.PixelWidth, src.PixelHeight);
        if (maxDim <= ThumbSize)
        {
            if (src.CanFreeze) src.Freeze();
            return src;
        }

        var scale = (double)ThumbSize / maxDim;
        var scaled = new TransformedBitmap(src, new ScaleTransform(scale, scale));
        scaled.Freeze();
        return scaled;
    }
}
