using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Viewer.Loaders;

// .gif needs to actually play, not just show frame 1. The first frame is still
// decoded here for the filmstrip thumbnail / fit-scale calc; playback itself is
// driven by the view via WpfAnimatedGif against the file path (see ImageCanvas).
public sealed class AnimatedGifLoader : IImageLoader
{
    public bool CanLoad(string extensionLower) => string.Equals(extensionLower, ".gif", StringComparison.OrdinalIgnoreCase);

    public Task<LoadedImage> LoadAsync(string path) => Task.Run(() =>
    {
        using var stream = File.OpenRead(path);
        var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var firstFrame = decoder.Frames[0];
        if (firstFrame.CanFreeze) firstFrame.Freeze();
        return new LoadedImage(firstFrame, isAnimatedGif: true, filePath: path);
    });
}
