using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Viewer.Services;

// One recognized line of text and its bounding box, in the SOURCE BITMAP's own
// pixel coordinates (not screen coordinates - the caller maps that, since it
// depends on the image's current on-screen position/zoom).
public sealed record OcrLine(string Text, Rect BoundingRect);

// Uses Windows' own built-in OCR engine (Windows.Media.Ocr) - no extra
// download/NuGet, works offline, and is already present on every Windows 10/11
// install (language packs may need adding via Settings, but the engine itself
// ships with the OS). The engine is only ever created on the first actual OCR
// request (see MainWindow.Ocr_Click) - never eagerly at startup.
public static class OcrService
{
    public static IReadOnlyList<OcrLine> RecognizeLines(BitmapSource source)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? TryAnyInstalledLanguage()
            ?? throw new InvalidOperationException(
                "Не установлен ни один языковой пакет для распознавания текста. " +
                "Добавьте его в Параметры Windows -> Время и язык -> Язык и регион.");

        var softwareBitmap = ToSoftwareBitmap(source);
        var result = engine.RecognizeAsync(softwareBitmap).GetAwaiter().GetResult();

        var lines = new List<OcrLine>();
        foreach (var line in result.Lines)
        {
            if (line.Words.Count == 0) continue;

            double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
            foreach (var word in line.Words)
            {
                var r = word.BoundingRect;
                left = Math.Min(left, r.X);
                top = Math.Min(top, r.Y);
                right = Math.Max(right, r.X + r.Width);
                bottom = Math.Max(bottom, r.Y + r.Height);
            }
            lines.Add(new OcrLine(line.Text, new Rect(left, top, right - left, bottom - top)));
        }
        return lines;
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

        // AsBuffer()'s IBuffer<->byte[] bridge is a plain BCL marshaller, not a
        // CsWinRT-projected COM interface - unlike the classic IMemoryBufferByteAccess
        // unsafe-interop pattern (which throws "Invalid cast from WinRT.IInspectable"
        // under this TFM's CsWinRT projections), this route just works.
        return SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
    }
}
