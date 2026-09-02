using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Viewer.Loaders;

// .procreate files are ZIP archives. We don't need the full NSKeyedArchiver
// Document.archive parse (layers, blend modes, etc. - that's editor territory)
// to VIEW one: Procreate itself writes a QuickLook/Preview.png composite
// (falling back to the lower-res QuickLook/Thumbnail.png) purely so Finder can
// preview the file without opening the app, and that's exactly what we want
// too. Reference: https://github.com/NothingData/ProcreateViewer.
//
// Timelapse video: Procreate can embed a recording of the drawing session as
// several segment files (in recording order) inside the archive. Detecting
// their presence here is just a name check (cheap - no bytes read), but
// actually joining and playing them is deferred entirely until the user
// presses Play (see EnsureJoinedVideo, called from MainWindow) - it's real
// work (I/O + concatenation) that shouldn't happen just because a file was
// opened.
public sealed class ProcreateImageLoader : IImageLoader
{
    private static readonly string[] VideoExtensions = { ".mov", ".mp4", ".m4v" };

    public bool CanLoad(string extensionLower) => string.Equals(extensionLower, ".procreate", StringComparison.OrdinalIgnoreCase);

    public Task<LoadedImage> LoadAsync(string path) => Task.Run(() =>
    {
        using var archive = ZipFile.OpenRead(path);

        var previewEntry =
            FindEntry(archive, "QuickLook/Preview.png") ??
            FindEntry(archive, "QuickLook/Thumbnail.png") ??
            archive.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("QuickLook/", StringComparison.OrdinalIgnoreCase) &&
                e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        if (previewEntry == null)
            throw new NotSupportedException("Procreate file has no QuickLook preview to display.");

        var bmp = new BitmapImage();
        using (var entryStream = previewEntry.Open())
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

        var hasVideo = archive.Entries.Any(e => VideoExtensions.Contains(Path.GetExtension(e.FullName), StringComparer.OrdinalIgnoreCase));
        return new LoadedImage(bmp, procreateVideoSourcePath: hasVideo ? path : null);
    });

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string name) =>
        archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Concatenates the recording's video segments (in the order they
    /// appear in the archive - the user confirmed that's their recording
    /// order) into one playable file, caching the result. Only ever called
    /// from a Play button press, never during load.</summary>
    public static string? EnsureJoinedVideo(string procreatePath)
    {
        using var archive = ZipFile.OpenRead(procreatePath);
        var segments = archive.Entries
            .Where(e => VideoExtensions.Contains(Path.GetExtension(e.FullName), StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (segments.Count == 0) return null;

        var cacheDir = Path.Combine(Path.GetTempPath(), "ViewerProcreateVideo");
        Directory.CreateDirectory(cacheDir);
        var stamp = File.GetLastWriteTimeUtc(procreatePath).Ticks;
        var ext = Path.GetExtension(segments[0].FullName);
        var outputPath = Path.Combine(cacheDir, $"{Path.GetFileNameWithoutExtension(procreatePath)}_{stamp}_joined{ext}");

        if (File.Exists(outputPath)) return outputPath;

        var tempPath = outputPath + ".tmp";
        using (var outStream = File.Create(tempPath))
        {
            foreach (var seg in segments)
            {
                using var segStream = seg.Open();
                segStream.CopyTo(outStream);
            }
        }
        File.Move(tempPath, outputPath, overwrite: true);
        return outputPath;
    }
}
