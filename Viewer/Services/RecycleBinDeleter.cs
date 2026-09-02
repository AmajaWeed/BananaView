using System;
using System.Runtime.InteropServices;

namespace Viewer.Services;

// Sends a file to the Recycle Bin (recoverable), same as Explorer's normal
// delete. Uses the classic shell32 SHFileOperation instead of pulling in a
// Windows Forms/VisualBasic reference just for this one call.
public static class RecycleBinDeleter
{
    private const int FO_DELETE = 3;
    private const ushort FOF_ALLOWUNDO = 0x40;
    private const ushort FOF_NOCONFIRMATION = 0x10;
    private const ushort FOF_SILENT = 0x4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public int wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

    public static bool Delete(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + '\0',
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT),
        };
        var result = SHFileOperation(ref op);
        return result == 0 && !op.fAnyOperationsAborted;
    }
}
