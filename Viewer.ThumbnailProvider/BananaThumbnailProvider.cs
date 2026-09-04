using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using ImageMagick;
using Viewer.Loaders;

namespace Viewer.ThumbnailProvider;

// Windows Explorer's thumbnail handler for the formats BananaView supports
// that Windows itself has no built-in decoder for: PSD, .procreate, .sai2,
// .icns. Registered per-extension via HKCR\<ext>\ShellEx\{e357fccd-...} (see
// installer/BananaView.iss and BananaViewSilent.iss) pointing at this class's
// CLSID; Explorer loads it out-of-proc (in a low-integrity surrogate, not
// explorer.exe itself) and calls IInitializeWithStream then IThumbnailProvider
// - a crash here can't take Explorer down with it.
//
// Format is sniffed from the stream's own magic bytes rather than trusted
// from the extension - Explorer hands us a raw stream, not a filename, and
// content-sniffing works fine here since the four formats have distinct,
// unambiguous signatures.
[ComVisible(true)]
[Guid("327b8523-1a5d-4c8d-9d60-611a8acf1572")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class BananaThumbnailProvider : IThumbnailProvider, IInitializeWithStream
{
    private const int E_FAIL = unchecked((int)0x80004005);

    private byte[]? _data;

    public int Initialize(IStream pstream, uint grfMode)
    {
        try
        {
            _data = ReadAllBytes(pstream);
            return 0; // S_OK
        }
        catch
        {
            _data = null;
            return E_FAIL;
        }
    }

    public int GetThumbnail(uint cx, out nint phbmp, out WTS_ALPHATYPE pdwAlpha)
    {
        phbmp = 0;
        pdwAlpha = WTS_ALPHATYPE.WTSAT_RGB;

        var data = _data;
        if (data == null || data.Length < 8) return E_FAIL;

        try
        {
            using var bmp = Decode(data, (int)Math.Max(16, Math.Min(cx, 1024)));
            if (bmp == null) return E_FAIL;

            phbmp = bmp.GetHbitmap();
            return 0; // S_OK
        }
        catch
        {
            return E_FAIL;
        }
    }

    private static Bitmap? Decode(byte[] data, int maxDim)
    {
        if (Matches(data, 0, "8BPS"))
            return DecodePsd(data, maxDim);

        if (Matches(data, 0, "icns"))
            return DecodeIcns(data, maxDim);

        if (data.Length >= 15 && Encoding.ASCII.GetString(data, 0, 15) == "SAI-CANVAS-TYPE")
            return DecodeSai2(data, maxDim);

        if (data[0] == 'P' && data[1] == 'K' && data[2] == 3 && data[3] == 4)
            return DecodeProcreate(data, maxDim);

        return null;
    }

    private static Bitmap DecodePsd(byte[] data, int maxDim)
    {
        using var image = new MagickImage(data);
        image.Thumbnail(new MagickGeometry((uint)maxDim, (uint)maxDim));
        return ToBitmap(image);
    }

    private static Bitmap? DecodeIcns(byte[] data, int maxDim)
    {
        var png = FindLargestIcnsPngChunk(data);
        if (png == null) return null;
        using var raw = new Bitmap(new MemoryStream(png));
        return FitToSquare(raw, maxDim);
    }

    private static Bitmap DecodeSai2(byte[] data, int maxDim)
    {
        // Sai2NativeDecoder only reads from a file path - route through a
        // scratch temp file rather than duplicating its parsing logic here.
        var tempPath = Path.Combine(Path.GetTempPath(), $"bananaview_thumb_{Guid.NewGuid():N}.sai2");
        try
        {
            File.WriteAllBytes(tempPath, data);
            var decoded = Sai2NativeDecoder.DecodeFlattened(tempPath);

            using var raw = new Bitmap(decoded.Width, decoded.Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, decoded.Width, decoded.Height);
            var bits = raw.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                // Both are already B,G,R,A byte order - a straight copy, no channel swap.
                Marshal.Copy(decoded.Bgra, 0, bits.Scan0, decoded.Bgra.Length);
            }
            finally
            {
                raw.UnlockBits(bits);
            }
            return FitToSquare(raw, maxDim);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
        }
    }

    private static Bitmap? DecodeProcreate(byte[] data, int maxDim)
    {
        using var ms = new MemoryStream(data);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        var entry =
            archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, "QuickLook/Thumbnail.png", StringComparison.OrdinalIgnoreCase)) ??
            archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, "QuickLook/Preview.png", StringComparison.OrdinalIgnoreCase)) ??
            archive.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("QuickLook/", StringComparison.OrdinalIgnoreCase) &&
                e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        if (entry == null) return null;

        using var entryStream = entry.Open();
        using var pngMs = new MemoryStream();
        entryStream.CopyTo(pngMs);
        pngMs.Position = 0;

        using var raw = new Bitmap(pngMs);
        return FitToSquare(raw, maxDim);
    }

    private static Bitmap ToBitmap(MagickImage image)
    {
        using var ms = new MemoryStream(image.ToByteArray(MagickFormat.Bmp));
        return new Bitmap(ms);
    }

    private static Bitmap FitToSquare(Bitmap source, int maxDim)
    {
        var maxSide = Math.Max(source.Width, source.Height);
        if (maxSide <= maxDim) return new Bitmap(source);

        var scale = (double)maxDim / maxSide;
        var w = Math.Max(1, (int)Math.Round(source.Width * scale));
        var h = Math.Max(1, (int)Math.Round(source.Height * scale));

        var scaled = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(scaled);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(source, 0, 0, w, h);
        return scaled;
    }

    private static bool Matches(byte[] data, int offset, string ascii) =>
        data.Length >= offset + ascii.Length && Encoding.ASCII.GetString(data, offset, ascii.Length) == ascii;

    // Same TLV-chunk PNG search as Viewer.Loaders.IcnsImageLoader, kept as its
    // own tiny copy rather than a cross-project link: it's ~15 lines with no
    // shared types, not worth the coupling.
    private static byte[]? FindLargestIcnsPngChunk(byte[] bytes)
    {
        byte[]? best = null;
        var offset = 8;
        while (offset + 8 <= bytes.Length)
        {
            var chunkLen = ((uint)bytes[offset + 4] << 24) | ((uint)bytes[offset + 5] << 16) | ((uint)bytes[offset + 6] << 8) | bytes[offset + 7];
            if (chunkLen < 8 || offset + chunkLen > bytes.Length) break;

            var dataStart = offset + 8;
            var dataLen = (int)chunkLen - 8;
            if (dataLen > 8 && bytes[dataStart] == 0x89 && bytes[dataStart + 1] == 'P' && bytes[dataStart + 2] == 'N' && bytes[dataStart + 3] == 'G'
                && (best == null || dataLen > best.Length))
            {
                best = new byte[dataLen];
                Array.Copy(bytes, dataStart, best, 0, dataLen);
            }

            offset += (int)chunkLen;
        }
        return best;
    }

    private static byte[] ReadAllBytes(IStream stream)
    {
        stream.Stat(out var stat, 0);
        var length = (int)stat.cbSize;
        var buffer = new byte[length];

        var read = 0;
        var chunk = new byte[81920];
        while (read < length)
        {
            var toRead = Math.Min(chunk.Length, length - read);
            unsafe
            {
                int bytesReadThisCall = 0;
                stream.Read(chunk, toRead, (nint)(&bytesReadThisCall));
                if (bytesReadThisCall <= 0) break;
                Array.Copy(chunk, 0, buffer, read, bytesReadThisCall);
                read += bytesReadThisCall;
            }
        }
        return read == length ? buffer : buffer[..read];
    }
}
