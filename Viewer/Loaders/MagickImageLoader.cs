using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace Viewer.Loaders;

// Everything WIC can't decode natively, via Magick.NET: PSD (flattened
// composite), WEBP, HEIC/HEIF, and ICO (picking the largest embedded frame -
// more forgiving than WIC's IconBitmapDecoder, which rejected some otherwise
// valid .ico files in testing). Magick.NET's native binaries bundle
// libwebp/libheif, no separate NuGets or the Windows Store "HEIF Image
// Extensions" needed. ICNS is NOT included here - Magick.NET's Windows build
// has no delegate for it at all; see IcnsImageLoader for that.
public sealed class MagickImageLoader : IImageLoader
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".psd", ".webp", ".heic", ".heif", ".ico", ".avif"
    };

    // Formats where the file holds multiple sizes/frames and we want the
    // largest one, rather than just "the first image".
    private static readonly HashSet<string> MultiFrameExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ico"
    };

    public bool CanLoad(string extensionLower) => Extensions.Contains(extensionLower);

    public Task<LoadedImage> LoadAsync(string path) => Task.Run(() =>
    {
        var ext = Path.GetExtension(path);
        byte[] pngBytes;

        if (MultiFrameExtensions.Contains(ext))
        {
            using var collection = new MagickImageCollection(path);
            var best = collection.OrderByDescending(i => i.Width).First();
            best.Format = MagickFormat.Png32;
            pngBytes = best.ToByteArray();
        }
        else
        {
            using var image = new MagickImage(path);
            image.Format = MagickFormat.Png32;
            pngBytes = image.ToByteArray();
        }

        var bmp = new BitmapImage();
        using (var ms = new MemoryStream(pngBytes))
        {
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
        }
        bmp.Freeze();
        return new LoadedImage(bmp);
    });
}
