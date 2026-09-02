using System.Windows.Media.Imaging;

namespace Viewer.Loaders;

public sealed class LoadedImage
{
    public BitmapSource Preview { get; }
    public bool IsAnimatedGif { get; }
    public string? FilePath { get; }

    /// <summary>The source .procreate file, if it has an embedded timelapse
    /// recording. The (possibly multi-segment) video is joined lazily - only
    /// when the user presses Play - not eagerly during load.</summary>
    public string? ProcreateVideoSourcePath { get; }

    public LoadedImage(BitmapSource preview, bool isAnimatedGif = false, string? filePath = null, string? procreateVideoSourcePath = null)
    {
        Preview = preview;
        IsAnimatedGif = isAnimatedGif;
        FilePath = filePath;
        ProcreateVideoSourcePath = procreateVideoSourcePath;
    }
}
