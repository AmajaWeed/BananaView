using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Viewer.Loaders;

// .sai2 is PaintTool SAI2's proprietary canvas format - no public spec. The
// primary path is now a native C# port of the user's Python reverse-
// engineering work (Sai2NativeDecoder, ported from sai2layers.py/sai2dpcm.py):
// decodes the file's own pre-flattened "intg" chunk straight to a BGRA buffer
// in memory, with no subprocess/temp-file/Python+numpy startup cost. If that
// ever throws (a file that trips something the port doesn't handle), this
// falls back to shelling out to sai2_to_png.py, which is slower but was the
// proven-working path before the native port.
public sealed class Sai2ImageLoader : IImageLoader
{
    public bool CanLoad(string extensionLower) => string.Equals(extensionLower, ".sai2", StringComparison.OrdinalIgnoreCase);

    public Task<LoadedImage> LoadAsync(string path) => Task.Run(() =>
    {
        try
        {
            var decoded = Sai2NativeDecoder.DecodeFlattened(path);
            var bmp = BitmapSource.Create(
                decoded.Width, decoded.Height, 96, 96,
                PixelFormats.Bgra32, null,
                decoded.Bgra, decoded.Width * 4);
            bmp.Freeze();
            return new LoadedImage(bmp);
        }
        catch (Exception nativeEx)
        {
            var pngPath = EnsureFlattenedPngViaPython(path)
                ?? throw new InvalidOperationException("Native .sai2 decode failed and no Python fallback was available.", nativeEx);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(pngPath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return new LoadedImage(bmp);
        }
    });

    // ---- Python fallback (kept only as a safety net - see class comment) ----

    private static string? EnsureFlattenedPngViaPython(string sai2Path)
    {
        var scriptDir = FindScriptDirectory();
        if (scriptDir == null) return null;

        var cacheDir = Path.Combine(Path.GetTempPath(), "ViewerSai2Cache");
        Directory.CreateDirectory(cacheDir);
        var stamp = File.GetLastWriteTimeUtc(sai2Path).Ticks;
        var outputPath = Path.Combine(cacheDir, $"{Path.GetFileNameWithoutExtension(sai2Path)}_{stamp}.png");
        if (File.Exists(outputPath)) return outputPath;

        var scriptPath = Path.Combine(scriptDir, "sai2_to_png.py");
        var psi = new ProcessStartInfo
        {
            FileName = "python",
            WorkingDirectory = scriptDir,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(sai2Path);
        psi.ArgumentList.Add(outputPath);

        using var proc = Process.Start(psi);
        if (proc == null) return null;
        proc.WaitForExit(60_000);
        return proc.ExitCode == 0 && File.Exists(outputPath) ? outputPath : null;
    }

    private static string? FindScriptDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "sai2_to_png.py")))
                return dir.FullName;
        }
        return null;
    }
}
