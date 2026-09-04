using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Viewer.Loaders;

// .kra files are ZIP archives (same family as .procreate) - Krita writes a
// full-resolution flattened "mergedimage.png" at the archive root, which is
// exactly what we want to display; "preview.png" (a smaller thumbnail) is
// the fallback for older files that don't have the merged image.
public sealed class KritaImageLoader : IImageLoader
{
    public bool CanLoad(string extensionLower) => string.Equals(extensionLower, ".kra", StringComparison.OrdinalIgnoreCase);

    public Task<LoadedImage> LoadAsync(string path) => Task.Run(() =>
    {
        using var archive = ZipFile.OpenRead(path);

        var entry =
            FindEntry(archive, "mergedimage.png") ??
            FindEntry(archive, "preview.png") ??
            throw new NotSupportedException("This .kra file has no mergedimage.png/preview.png to display.");

        var bmp = new BitmapImage();
        using (var entryStream = entry.Open())
        using (var ms = new MemoryStream())
        {
            entryStream.CopyTo(ms);
            ms.Position = 0;
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
        }
        bmp.Freeze();
        return new LoadedImage(bmp);
    });

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string name) =>
        archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, name, StringComparison.OrdinalIgnoreCase));
}
