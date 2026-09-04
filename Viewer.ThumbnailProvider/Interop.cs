using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Viewer.ThumbnailProvider;

// Well-known Windows Shell interfaces (thumbcache.h / propsys.h) - Pascal
// Script has nothing to do with this project, but same principle as the
// installer's [Code] section: these GUIDs are load-bearing and must match
// Microsoft's exactly, or Explorer silently never calls in at all.

public enum WTS_ALPHATYPE
{
    WTSAT_UNKNOWN = 0,
    WTSAT_RGB = 1,
    WTSAT_ARGB = 2,
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("e357fccd-a995-4576-b01f-234630154e96")]
public interface IThumbnailProvider
{
    [PreserveSig]
    int GetThumbnail(uint cx, out nint phbmp, out WTS_ALPHATYPE pdwAlpha);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("b824b49d-22ac-4161-ac8a-9916e8fa3f7f")]
public interface IInitializeWithStream
{
    [PreserveSig]
    int Initialize(IStream pstream, uint grfMode);
}
