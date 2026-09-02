using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Viewer.Loaders;
using Viewer.Services;
using Viewer.ViewModels;
using WpfAnimatedGif;

namespace Viewer;

public partial class MainWindow : Window
{
    // Segoe MDL2 Assets glyphs (Private Use Area codepoints).
    private const string PlayGlyph = "";
    private const string PauseGlyph = "";
    private const string PinGlyph = "";
    private const string UnpinGlyph = "";
    private static readonly System.Windows.Media.Brush PinActiveBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x9E, 0xFF));
    private static readonly System.Windows.Media.Brush PinInactiveBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE6, 0xE6, 0xE6));

    private readonly ImageLoaderRegistry _registry = new();
    private readonly FolderCatalog _catalog;
    private readonly ThumbnailCache _thumbCache;
    private readonly SlideshowTimer _slideshow = new();
    private readonly MainViewModel _vm = new();
    private readonly DispatcherTimer _hideTimer;

    private bool _fullscreen;
    private WindowState _preFullscreenState = WindowState.Maximized;
    private bool _windowed;
    private int _loadToken;
    private string? _procreateVideoSourcePath;
    private bool _videoPlaying;
    private LoadedImage? _currentImage;

    public MainWindow()
    {
        InitializeComponent();
        _catalog = new FolderCatalog(_registry);
        _thumbCache = new ThumbnailCache(_registry);
        FilmstripList.ItemsSource = _vm.Items;

        _slideshow.Tick += () => Dispatcher.Invoke(() => Navigate(+1));

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _hideTimer.Tick += (_, _) => HideOverlayIfIdle();

        TitlePinButton.Content = PinGlyph;
        TopPinButton.Content = PinGlyph;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var args = Environment.GetCommandLineArgs();
        var path = args.Skip(1).FirstOrDefault(a => File.Exists(a) && _registry.IsSupported(Path.GetExtension(a)));
        if (path != null) OpenFile(path);
        else ShowOverlay();
    }

    // ---- Fix: a WindowStyle="None" window's WindowState="Maximized" covers the
    // whole screen INCLUDING the taskbar in WPF, instead of respecting the work
    // area like a normal window does. Intercepting WM_GETMINMAXINFO and filling
    // in the real work-area bounds is the standard fix - this is what was
    // letting the Windows taskbar visually sit on top of our bottom toolbar. ----

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref info);
            var work = info.rcWork;
            var mon = info.rcMonitor;

            mmi.ptMaxPosition.X = work.Left - mon.Left;
            mmi.ptMaxPosition.Y = work.Top - mon.Top;
            mmi.ptMaxSize.X = work.Right - work.Left;
            mmi.ptMaxSize.Y = work.Bottom - work.Top;

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        return IntPtr.Zero;
    }

    // ---- Opening / navigation ----------------------------------------

    private void OpenFile(string path)
    {
        _catalog.LoadFolder(path);
        EmptyHint.Visibility = Visibility.Collapsed;
        RebuildFilmstrip();
        LoadCurrentAsync(0);
    }

    private void Navigate(int direction)
    {
        if (_catalog.Files.Count == 0) return;
        if (direction > 0) _catalog.Next(); else _catalog.Previous();
        LoadCurrentAsync(direction);
        SyncFilmstripSelection();
    }

    private async void LoadCurrentAsync(int direction)
    {
        var path = _catalog.CurrentFile;
        if (path == null) return;

        var token = ++_loadToken;
        try
        {
            var image = await _registry.LoadAsync(path);
            if (token != _loadToken) return; // superseded by a newer navigation

            // Leaving any in-app video playback behind when the image changes.
            StopVideoPlayback();

            Canvas.SetImage(image, direction);
            _currentImage = image;
            _procreateVideoSourcePath = image.ProcreateVideoSourcePath;

            // Play/pause is for animated content: a gif animates in-place; a
            // Procreate timelapse plays in-app, its segments joined lazily on
            // the first press (see Play_Click) rather than during this load.
            // Hidden entirely for plain stills.
            if (image.IsAnimatedGif)
            {
                PlayButton.Visibility = Visibility.Visible;
                PlayButton.Content = PauseGlyph; // freshly loaded gif always starts playing
            }
            else if (image.ProcreateVideoSourcePath != null)
            {
                PlayButton.Visibility = Visibility.Visible;
                PlayButton.Content = PlayGlyph;
            }
            else
            {
                PlayButton.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            // Unsupported/corrupt file: skip silently rather than block navigation.
        }
    }

    private void RebuildFilmstrip()
    {
        // Unsubscribe BEFORE touching Items: ListBox can raise SelectionChanged
        // as items are added one by one (its selection can shift as the list
        // grows from empty), and each of those would have called
        // LoadCurrentAsync/_catalog.JumpTo - this is what was silently cycling
        // through several files right after opening one, before the intended
        // file's own load even had a chance to "win" the race.
        FilmstripList.SelectionChanged -= FilmstripList_SelectionChanged;

        _vm.Items.Clear();
        foreach (var file in _catalog.Files)
            _vm.Items.Add(new FilmstripItem(file));

        SyncFilmstripSelection(); // re-subscribes once everything has settled
        _ = LoadThumbnailsAsync();
    }

    private async Task LoadThumbnailsAsync()
    {
        foreach (var item in _vm.Items.ToArray())
        {
            var thumb = await _thumbCache.GetThumbnailAsync(item.Path);
            item.Thumbnail = thumb;
        }
    }

    private void SyncFilmstripSelection()
    {
        for (int i = 0; i < _vm.Items.Count; i++)
            _vm.Items[i].IsCurrent = i == _catalog.CurrentIndex;

        FilmstripList.SelectionChanged -= FilmstripList_SelectionChanged;
        FilmstripList.SelectedIndex = _catalog.CurrentIndex;
        if (FilmstripList.SelectedItem != null)
            FilmstripList.ScrollIntoView(FilmstripList.SelectedItem);
        FilmstripList.SelectionChanged += FilmstripList_SelectionChanged;
    }

    private void FilmstripList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var index = FilmstripList.SelectedIndex;
        if (index < 0 || index == _catalog.CurrentIndex) return;
        var direction = index > _catalog.CurrentIndex ? 1 : -1;
        _catalog.JumpTo(index);
        LoadCurrentAsync(direction);
        SyncFilmstripSelection();
    }

    // ---- Toolbar --------------------------------------------------------

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => Canvas.ZoomIn();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => Canvas.ZoomOut();
    private void Fit_Click(object sender, RoutedEventArgs e) => Canvas.ZoomToFit();
    private void RotateCW_Click(object sender, RoutedEventArgs e) => Canvas.RotateBy(90);
    private void RotateCCW_Click(object sender, RoutedEventArgs e) => Canvas.RotateBy(-90);

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null) return;
        try
        {
            Clipboard.SetImage(_currentImage.Preview);
        }
        catch
        {
            // Clipboard can be transiently locked by another app - not fatal.
        }
    }
    private void Prev_Click(object sender, RoutedEventArgs e) => Navigate(-1);
    private void Next_Click(object sender, RoutedEventArgs e) => Navigate(+1);

    // The toolbar play/pause button is only visible for animated content
    // (see LoadCurrentAsync) and controls THAT content's own playback -
    // slideshow (browsing photos automatically) is a separate feature, only
    // reachable via Space, so it doesn't fight over the same button/icon.
    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_procreateVideoSourcePath != null)
        {
            if (_videoPlaying)
            {
                VideoPlayer.Pause();
                _videoPlaying = false;
                PlayButton.Content = PlayGlyph;
                return;
            }

            // First press: join the recording's segments (or reuse the cached
            // joined file from a previous press) - deliberately NOT done at
            // load time, only here, on demand.
            if (VideoPlayer.Source == null)
            {
                var sourcePath = _procreateVideoSourcePath;
                ShowLoading("Склеивание таймлапса...");
                string? joinedPath;
                try
                {
                    joinedPath = await Task.Run(() => ProcreateImageLoader.EnsureJoinedVideo(sourcePath));
                }
                finally
                {
                    HideLoading();
                }

                if (sourcePath != _procreateVideoSourcePath) return; // navigated away while joining
                if (joinedPath == null) return;

                VideoPlayer.Source = new Uri(joinedPath);
                VideoPlayer.Visibility = Visibility.Visible;
            }

            VideoPlayer.Play();
            _videoPlaying = true;
            PlayButton.Content = PauseGlyph;
            return;
        }

        Canvas.ToggleGifPlayback();
        PlayButton.Content = Canvas.IsGifPaused ? PlayGlyph : PauseGlyph;
    }

    private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Position = TimeSpan.Zero;
        VideoPlayer.Play();
    }

    private void StopVideoPlayback()
    {
        if (VideoPlayer.Source != null)
        {
            VideoPlayer.Stop();
            VideoPlayer.Close();
            VideoPlayer.Source = null;
        }
        VideoPlayer.Visibility = Visibility.Collapsed;
        _videoPlaying = false;
    }

    private async void Ocr_Click(object sender, RoutedEventArgs e)
    {
        var image = _currentImage;
        if (image == null) return;

        ShowLoading("Распознавание текста...");
        try
        {
            var text = await Task.Run(() => OcrService.RecognizeText(image.Preview));
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowToast("Текст не найден");
                return;
            }
            Clipboard.SetText(text);
            ShowToast("Текст скопирован в буфер обмена");
        }
        catch (Exception ex)
        {
            ShowToast(ex.Message);
        }
        finally
        {
            HideLoading();
        }
    }

    private void ShowLoading(string message)
    {
        LoadingText.Text = message;
        LoadingOverlay.Visibility = Visibility.Visible;
        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, spin);
    }

    private void HideLoading()
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
        SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBorder.BeginAnimation(UIElement.OpacityProperty, null);
        var sb = new Storyboard();
        var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150));
        Storyboard.SetTarget(fadeIn, ToastBorder);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(fadeIn);

        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(400))
        {
            BeginTime = TimeSpan.FromSeconds(2.2)
        };
        Storyboard.SetTarget(fadeOut, ToastBorder);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(fadeOut);

        sb.Begin();
    }

    private void ToggleSlideshow() => _slideshow.Toggle();

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var path = _catalog.CurrentFile;
        if (path == null) return;

        if (RecycleBinDeleter.Delete(path))
        {
            var index = _catalog.CurrentIndex;
            _catalog.RemoveCurrent();
            if (index >= 0 && index < _vm.Items.Count) _vm.Items.RemoveAt(index);

            if (_catalog.CurrentFile != null)
            {
                LoadCurrentAsync(0);
                SyncFilmstripSelection();
            }
            else
            {
                EmptyHint.Visibility = Visibility.Visible;
            }
        }
    }

    // ---- Window chrome ----------------------------------------------

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        var glyph = Topmost ? UnpinGlyph : PinGlyph;
        var brush = Topmost ? PinActiveBrush : PinInactiveBrush;
        TitlePinButton.Content = glyph;
        TitlePinButton.Foreground = brush;
        TopPinButton.Content = glyph;
        TopPinButton.Foreground = brush;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, new RoutedEventArgs());
            return;
        }
        DragMove();
    }

    // Clicking the translucent background (outside the image itself) drops the
    // borderless "Picasa" viewer back into a normal, resizable window - the
    // presentation this app originally had before the fullscreen overlay look.
    private void Canvas_BackgroundClicked(object sender, EventArgs e)
    {
        if (_windowed) return;
        _windowed = true;

        // Snap the new window to exactly frame the image at its current
        // position/zoom, instead of always dropping into a fixed 1200x800 -
        // "the window neatly crops down to the image, right where it is."
        const double titleBarHeight = 44;
        var imageBounds = Canvas.GetImageBoundsRelativeToControl();

        double width, height, screenLeft, screenTop;
        if (!imageBounds.IsEmpty)
        {
            var topLeftScreen = Canvas.PointToScreen(imageBounds.TopLeft);
            width = Math.Clamp(imageBounds.Width, 300, SystemParameters.WorkArea.Width);
            height = Math.Clamp(imageBounds.Height, 200, SystemParameters.WorkArea.Height - titleBarHeight);
            screenLeft = topLeftScreen.X;
            screenTop = topLeftScreen.Y - titleBarHeight;
        }
        else
        {
            width = 1200;
            height = 800;
            screenLeft = (SystemParameters.WorkArea.Width - width) / 2;
            screenTop = (SystemParameters.WorkArea.Height - height) / 2;
        }

        TopOverlayPanel.Visibility = Visibility.Collapsed;
        TitleRow.Height = new GridLength(titleBarHeight);
        ContentGrid.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x14, 0x14, 0x14));

        ResizeMode = ResizeMode.CanResize;
        WindowState = WindowState.Normal;
        Width = width;
        Height = height + titleBarHeight;
        Left = screenLeft;
        Top = screenTop;
    }

    private void ToggleFullscreen()
    {
        _fullscreen = !_fullscreen;
        if (_fullscreen)
        {
            // WindowState.Maximized always respects the taskbar's work area, so
            // true edge-to-edge fullscreen needs manual bounds instead.
            _preFullscreenState = WindowState;
            WindowState = WindowState.Normal;
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
        }
        else
        {
            WindowState = _preFullscreenState;
        }
    }

    // ---- Overlay auto-hide --------------------------------------------

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        ShowOverlay();
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void ShowOverlay()
    {
        OverlayPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
        TopOverlayPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
    }

    private void HideOverlayIfIdle()
    {
        _hideTimer.Stop();
        if (OverlayPanel.IsMouseOver || TopOverlayPanel.IsMouseOver) return;
        OverlayPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(300)));
        TopOverlayPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(300)));
    }

    // ---- Drag & drop ------------------------------------------------

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var path = files.FirstOrDefault(f => File.Exists(f) && _registry.IsSupported(Path.GetExtension(f)));
        if (path != null) OpenFile(path);
    }

    // ---- Keyboard shortcuts --------------------------------------------

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                Navigate(-1); break;
            case Key.Right:
                Navigate(+1); break;
            case Key.Home:
                if (_catalog.Files.Count > 0) { _catalog.JumpTo(0); LoadCurrentAsync(-1); SyncFilmstripSelection(); }
                break;
            case Key.End:
                if (_catalog.Files.Count > 0) { _catalog.JumpTo(_catalog.Files.Count - 1); LoadCurrentAsync(1); SyncFilmstripSelection(); }
                break;
            case Key.Add:
            case Key.OemPlus:
                Canvas.ZoomIn(); break;
            case Key.Subtract:
            case Key.OemMinus:
                Canvas.ZoomOut(); break;
            case Key.D0:
            case Key.NumPad0:
                Canvas.ZoomToFit(); break;
            case Key.D1:
            case Key.NumPad1:
                Canvas.ZoomTo100(); break;
            case Key.F:
            case Key.F11:
                ToggleFullscreen(); break;
            case Key.Space:
                ToggleSlideshow(); break;
            case Key.Delete:
                Delete_Click(this, new RoutedEventArgs()); break;
            case Key.R:
                Canvas.RotateBy(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -90 : 90); break;
            case Key.Escape:
                if (_fullscreen) ToggleFullscreen(); else Close();
                break;
        }
    }
}
