using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Viewer.Loaders;

// Formats WIC decodes natively: no extra dependency needed.
// (.ico is handled by MagickImageLoader - WIC's IconBitmapDecoder rejected
// some otherwise-valid .ico files in testing.)
public sealed class RasterImageLoader : IImageLoader
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".tif", ".tiff"
    };

    public bool CanLoad(string extensionLower) => Extensions.Contains(extensionLower);

    public Task<LoadedImage> LoadAsync(string path) => Task.Run(() =>
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.EndInit();

        if (bmp.CanFreeze) bmp.Freeze();
        return new LoadedImage(bmp);
    });
}
