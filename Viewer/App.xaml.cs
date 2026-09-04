using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace Viewer;

public partial class App : Application
{
    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BananaView", "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Bugs that don't reproduce here (this session's development
        // machine) have been a recurring problem - nothing was ever written
        // down about what actually happened on the user's end. This doesn't
        // fix that on its own, but it means a future crash at least leaves
        // something to look at instead of nothing.
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("DispatcherUnhandledException", args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) LogCrash("AppDomain.UnhandledException", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogCrash("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);
    }

    private static void LogCrash(string source, Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n";
            File.AppendAllText(LogPath, entry);
        }
        catch
        {
            // Logging the crash failed too - nothing more we can do.
        }
    }
}

public sealed class BoolToHighlightConverter : IValueConverter
{
    private static readonly Brush Highlight = new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF));
    private static readonly Brush None = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Highlight : None;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
