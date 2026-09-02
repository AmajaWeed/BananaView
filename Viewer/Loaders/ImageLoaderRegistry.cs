using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Viewer.Loaders;

public sealed class ImageLoaderRegistry
{
    // Order matters: AnimatedGifLoader must claim .gif before any generic fallback.
    private readonly IImageLoader[] _loaders =
    {
        new AnimatedGifLoader(),
        new RasterImageLoader(),
        new IcnsImageLoader(),
        new MagickImageLoader(),
        new ProcreateImageLoader(),
        new Sai2ImageLoader(),
    };

    public bool IsSupported(string extension) => _loaders.Any(l => l.CanLoad(extension));

    public IImageLoader? GetLoader(string extension) => _loaders.FirstOrDefault(l => l.CanLoad(extension));

    public Task<LoadedImage> LoadAsync(string path)
    {
        var ext = Path.GetExtension(path);
        var loader = GetLoader(ext) ?? throw new NotSupportedException($"Unsupported format: {ext}");
        return loader.LoadAsync(path);
    }
}
