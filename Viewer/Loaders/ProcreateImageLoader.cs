using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Viewer.Services;

namespace Viewer.Loaders;

// .procreate files are ZIP archives. We don't need the full NSKeyedArchiver
// Document.archive parse (layers, blend modes, etc. - that's editor territory)
// to VIEW one: Procreate itself writes a QuickLook/Preview.png composite
// (falling back to the lower-res QuickLook/Thumbnail.png) purely so Finder can
// preview the file without opening the app, and that's exactly what we want
// too. Reference: https://github.com/NothingData/ProcreateViewer.
//
// Timelapse video: Procreate can embed a recording of the drawing session as
// several segment files inside the archive. Detecting their presence here is
// just a name check (cheap - no bytes read); extracting them is deferred
// entirely until the user presses Play (see EnsureVideoSegments, called from
// MainWindow) - it's real work (I/O) that shouldn't happen just because a
// file was opened.
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

    /// <summary>Extracts every recording segment to its own cached file, in
    /// natural filename order (segment-2 before segment-10), and returns
    /// their paths in play order. Raw byte concatenation of independent
    /// MOV/MP4 files does NOT produce a valid combined video - each segment
    /// is its own complete container with its own index, so a player just
    /// stops at the end of the first one and ignores the rest (the "only a
    /// short part of one segment plays" bug). Playing the segments back to
    /// back instead - MainWindow swaps MediaElement.Source on MediaEnded -
    /// gives the same continuous-timelapse result without needing to
    /// re-mux anything. Only ever called from a Play button press, never
    /// during load.</summary>
    public static string[] EnsureVideoSegments(string procreatePath)
    {
        using var archive = ZipFile.OpenRead(procreatePath);
        var segments = archive.Entries
            .Where(e => VideoExtensions.Contains(Path.GetExtension(e.FullName), StringComparer.OrdinalIgnoreCase))
            .OrderBy(e => e.Name, NaturalStringComparer.Instance)
            .ToList();
        if (segments.Count == 0) return Array.Empty<string>();

        var stamp = File.GetLastWriteTimeUtc(procreatePath).Ticks;
        var cacheDir = Path.Combine(Path.GetTempPath(), "ViewerProcreateVideo", $"{Path.GetFileNameWithoutExtension(procreatePath)}_{stamp}");
        Directory.CreateDirectory(cacheDir);

        var paths = new string[segments.Count];
        for (var i = 0; i < segments.Count; i++)
        {
            var outputPath = Path.Combine(cacheDir, $"{i:D4}{Path.GetExtension(segments[i].FullName)}");
            if (!File.Exists(outputPath))
            {
                var tempPath = outputPath + ".tmp";
                using (var outStream = File.Create(tempPath))
                using (var segStream = segments[i].Open())
                    segStream.CopyTo(outStream);
                File.Move(tempPath, outputPath, overwrite: true);
            }
            paths[i] = outputPath;
        }
        return paths;
    }
}
