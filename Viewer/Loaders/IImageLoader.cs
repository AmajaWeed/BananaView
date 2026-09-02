using System.Threading.Tasks;

namespace Viewer.Loaders;

// Extensibility seam: future SAI2 / Procreate / CLIP loaders implement this
// and register in ImageLoaderRegistry without touching any UI code.
public interface IImageLoader
{
    bool CanLoad(string extensionLower);
    Task<LoadedImage> LoadAsync(string path);
}
