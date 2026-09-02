using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Viewer.Loaders;

// Magick.NET's Windows NuGet distribution doesn't include a delegate for
// .icns at all (confirmed via MagickMissingDelegateErrorException), so this
// is a small standalone parser instead. Modern .icns files (macOS 10.7+,
// which covers virtually everything encountered today) store their larger
// icon sizes as plain embedded PNGs inside otherwise-opaque TLV chunks - we
// don't need to understand the full Apple Icon Image format, just find the
// biggest PNG payload and hand it to WIC. Legacy raw-bitmap/RLE-only icns
// files (pre-10.7) aren't supported; they're rare enough today to not be
// worth a bespoke decoder for a viewer.
public sealed class IcnsImageLoader : IImageLoader
{
    public bool CanLoad(string extensionLower) => string.Equals(extensionLower, ".icns", StringComparison.OrdinalIgnoreCase);

    public Task<LoadedImage> LoadAsync(string path) => Task.Run(() =>
    {
        var png = FindLargestPngChunk(File.ReadAllBytes(path))
            ?? throw new NotSupportedException("This .icns file has no embedded PNG icon (only legacy raw-bitmap icons, which aren't supported).");

        var bmp = new BitmapImage();
        using (var ms = new MemoryStream(png))
        {
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
        }
        bmp.Freeze();
        return new LoadedImage(bmp);
    });

    public static byte[]? FindLargestPngChunk(byte[] bytes)
    {
        if (bytes.Length < 8 || bytes[0] != 'i' || bytes[1] != 'c' || bytes[2] != 'n' || bytes[3] != 's')
            return null;

        byte[]? best = null;
        var offset = 8; // skip the "icns" + total-length header
        while (offset + 8 <= bytes.Length)
        {
            var chunkLen = ReadBigEndianUInt32(bytes, offset + 4);
            if (chunkLen < 8 || offset + chunkLen > bytes.Length) break;

            var dataStart = offset + 8;
            var dataLen = (int)chunkLen - 8;
            if (dataLen > 8 && IsPngSignature(bytes, dataStart) && (best == null || dataLen > best.Length))
            {
                best = new byte[dataLen];
                Array.Copy(bytes, dataStart, best, 0, dataLen);
            }

            offset += (int)chunkLen;
        }
        return best;
    }

    private static uint ReadBigEndianUInt32(byte[] b, int i) =>
        ((uint)b[i] << 24) | ((uint)b[i + 1] << 16) | ((uint)b[i + 2] << 8) | b[i + 3];

    private static bool IsPngSignature(byte[] b, int i) =>
        b[i] == 0x89 && b[i + 1] == (byte)'P' && b[i + 2] == (byte)'N' && b[i + 3] == (byte)'G';
}
