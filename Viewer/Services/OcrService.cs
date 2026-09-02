using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Viewer.Services;

// Uses Windows' own built-in OCR engine (Windows.Media.Ocr) - no extra
// download/NuGet, works offline, and is already present on every Windows 10/11
// install (language packs may need adding via Settings, but the engine itself
// ships with the OS). The engine is only ever created on the first actual OCR
// request (see MainWindow.Ocr_Click) - never eagerly at startup.
public static class OcrService
{
    public static string RecognizeText(BitmapSource source)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? TryAnyInstalledLanguage()
            ?? throw new InvalidOperationException(
                "Не установлен ни один языковой пакет для распознавания текста. " +
                "Добавьте его в Параметры Windows -> Время и язык -> Язык и регион.");

        var softwareBitmap = ToSoftwareBitmap(source);
        var result = engine.RecognizeAsync(softwareBitmap).GetAwaiter().GetResult();
        return result.Text;
    }

    private static OcrEngine? TryAnyInstalledLanguage()
    {
        foreach (var lang in OcrEngine.AvailableRecognizerLanguages)
        {
            var engine = OcrEngine.TryCreateFromLanguage(lang);
            if (engine != null) return engine;
        }
        return null;
    }

    private static SoftwareBitmap ToSoftwareBitmap(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var w = converted.PixelWidth;
        var h = converted.PixelHeight;
        var stride = w * 4;
        var pixels = new byte[stride * h];
        converted.CopyPixels(pixels, stride, 0);

        var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
        using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Write);
        using var reference = buffer.CreateReference();
        unsafe
        {
            ((IMemoryBufferByteAccess)reference).GetBuffer(out var dataPtr, out var capacity);
            Marshal.Copy(pixels, 0, (IntPtr)dataPtr, Math.Min(pixels.Length, (int)capacity));
        }
        return bitmap;
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
