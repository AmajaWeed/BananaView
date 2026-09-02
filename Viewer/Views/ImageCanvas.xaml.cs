using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Viewer.Loaders;
using WpfAnimatedGif;

namespace Viewer.Views;

public partial class ImageCanvas : UserControl
{
    private const double MinScale = 0.05;
    private const double MaxScale = 20.0;
    private const double WheelStep = 1.15;

    // How fast the displayed value chases the target, in seconds ("time
    // constant" of an exponential decay - smaller = snappier).
    private const double SmoothingTau = 0.09;
    private const double SnapEpsilon = 0.0005;

    private double _fitScale = 1.0;
    private LoadedImage? _current;
    private bool _panning;
    private Point _panStartMouse;
    private Point _panStartTranslate;

    // The live displayed scale. Applied directly to MainImage.Width/Height
    // rather than via a RenderTransform ScaleTransform - see the comment in
    // ImageCanvas.xaml on why.
    private double _currentScale = 1.0;
    private double _targetScale = 1.0;
    private double _targetTranslateX;
    private double _targetTranslateY;
    private bool _smoothingActive;
    private DateTime _lastTick;
    private bool _gifPaused;

    public bool IsGifPaused => _gifPaused;

    public void ToggleGifPlayback()
    {
        var controller = ImageBehavior.GetAnimationController(MainImage);
        if (controller == null) return;
        _gifPaused = !_gifPaused;
        if (_gifPaused) controller.Pause(); else controller.Play();
    }

    /// <summary>Raised on a left click that lands outside the image's own rendered bounds.</summary>
    public event EventHandler? BackgroundClicked;

    public ImageCanvas()
    {
        InitializeComponent();
        SizeChanged += (_, _) => OnContainerResized();
        MouseWheel += OnMouseWheel;
        // Preview (tunneling) events fire before anything else can mark the
        // routed event Handled on the way down, and are captured-input-aware
        // regardless of what's under the cursor - the most robust way to make
        // sure every MouseMove during a drag actually reaches this handler.
        PreviewMouseLeftButtonDown += OnMouseDown;
        PreviewMouseLeftButtonUp += OnMouseUp;
        PreviewMouseMove += OnMouseMove;
        MouseDoubleClick += OnDoubleClick;
        LostMouseCapture += (_, _) => _panning = false;
    }

    /// <summary>direction: -1 = came from "previous", +1 = came from "next", 0 = no slide (initial open).</summary>
    public void SetImage(LoadedImage image, int direction = 0)
    {
        var hadPrevious = MainImage.Opacity > 0 &&
            (MainImage.Source != null || ImageBehavior.GetAnimatedSource(MainImage) != null);

        if (hadPrevious)
        {
            // Snapshot the current rotation + pan (size is already baked into
            // Width/Height, no scale transform to capture) so the outgoing
            // image doesn't visually jump before it fades out.
            var snapshot = new TransformGroup();
            snapshot.Children.Add(new RotateTransform(RotateT.Angle));
            snapshot.Children.Add(new TranslateTransform(TranslateT.X, TranslateT.Y));

            OutgoingImage.Source = MainImage.Source;
            OutgoingImage.Width = MainImage.Width;
            OutgoingImage.Height = MainImage.Height;
            Canvas.SetLeft(OutgoingImage, Canvas.GetLeft(MainImage));
            Canvas.SetTop(OutgoingImage, Canvas.GetTop(MainImage));
            ImageBehavior.SetAnimatedSource(OutgoingImage, ImageBehavior.GetAnimatedSource(MainImage));
            OutgoingImage.RenderTransform = snapshot;
            OutgoingImage.BeginAnimation(OpacityProperty, null);
            OutgoingImage.Opacity = 1;
        }

        _gifPaused = false;

        _current = image;

        if (image.IsAnimatedGif && image.FilePath != null)
        {
            var animSource = new BitmapImage();
            animSource.BeginInit();
            animSource.CacheOption = BitmapCacheOption.OnLoad;
            animSource.UriSource = new Uri(image.FilePath, UriKind.Absolute);
            animSource.EndInit();
            ImageBehavior.SetAnimatedSource(MainImage, animSource);
        }
        else
        {
            ImageBehavior.SetAnimatedSource(MainImage, null);
            MainImage.Source = image.Preview;
        }

        StopSmoothing();
        RotateT.BeginAnimation(RotateTransform.AngleProperty, null);
        RotateT.Angle = 0;
        MainImage.BeginAnimation(OpacityProperty, null);
        TranslateT.BeginAnimation(TranslateTransform.XProperty, null);

        void ApplyFitAndAnimateIn()
        {
            RecalculateFit();
            _currentScale = _fitScale;
            _targetScale = _fitScale;
            _targetTranslateX = 0;
            _targetTranslateY = 0;
            ApplyScaleToSize();
            TranslateT.X = direction * 40;
            TranslateT.Y = 0;
            MainImage.Opacity = 0;

            // Both animations below specify an explicit "From", so they don't
            // depend on a prior render pass having already painted the reset
            // state - starting them synchronously (instead of deferring via
            // Dispatcher.InvokeAsync) shaves off a frame of latency, which
            // matters when arrow keys are pressed in quick succession.
            var duration = TimeSpan.FromMilliseconds(220);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            // FillBehavior.Stop + pre-setting the base value to the animation's
            // own end target means the animation clock cleanly detaches the
            // instant it finishes (reverting to a base value that already
            // matches, so no visible jump) - no dependency on a Completed
            // handler racing a newer SetImage call that might fire first and
            // start its own animation on the same property (which is what was
            // occasionally causing a visible backward jerk during fast
            // next/prev navigation).
            var opacityIn = new DoubleAnimation(0, 1, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
            MainImage.Opacity = 1;
            MainImage.BeginAnimation(OpacityProperty, opacityIn);

            var slideIn = new DoubleAnimation(direction * 40, 0, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
            TranslateT.X = 0;
            TranslateT.BeginAnimation(TranslateTransform.XProperty, slideIn);

            if (hadPrevious)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200)) { FillBehavior = FillBehavior.Stop };
                fadeOut.Completed += (_, _) =>
                {
                    OutgoingImage.Source = null;
                    ImageBehavior.SetAnimatedSource(OutgoingImage, null);
                };
                OutgoingImage.Opacity = 0;
                OutgoingImage.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        // The container might not have been measured yet (e.g. very first image
        // right after the window opens) - defer one layout pass so the fit-scale
        // math has real ActualWidth/ActualHeight to work with, instead of
        // silently keeping the previous (wrong) fit scale.
        if (ActualWidth < 50 || ActualHeight < 50)
            Dispatcher.InvokeAsync(ApplyFitAndAnimateIn, DispatcherPriority.Loaded);
        else
            ApplyFitAndAnimateIn();

        // Safety net: on a freshly-opened window (especially maximized -
        // WM_GETMINMAXINFO can still be settling the final bounds a moment
        // after this control already reported a size), re-validate the fit
        // once everything else has finished settling, and snap to the
        // corrected value if the container's real size turned out different
        // from what we used above.
        var imageForSelfCheck = image;
        Dispatcher.InvokeAsync(() =>
        {
            if (_current != imageForSelfCheck) return; // navigated away already
            var before = _fitScale;
            RecalculateFit();
            if (Math.Abs(_fitScale - before) > 0.01 && Math.Abs(_targetScale - before) < 0.01)
            {
                _currentScale = _fitScale;
                _targetScale = _fitScale;
                ApplyScaleToSize();
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    public void ZoomIn() => SetZoomTarget(_targetScale * WheelStep, CenterPoint());
    public void ZoomOut() => SetZoomTarget(_targetScale / WheelStep, CenterPoint());
    public void ZoomTo100() => SetZoomTarget(1.0, CenterPoint());

    public void ZoomToFit()
    {
        RecalculateFit();
        _targetScale = _fitScale;
        _targetTranslateX = 0;
        _targetTranslateY = 0;
        StartSmoothing();
    }

    public void RotateBy(double degrees)
    {
        var target = RotateT.Angle + degrees;
        RotateT.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(target, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase() });
    }

    private Point CenterPoint() => new(ActualWidth / 2, ActualHeight / 2);

    // Bakes _currentScale into the image's actual layout size, then positions
    // it (centered) via Canvas.Left/Top - never via alignment-based Grid
    // arrangement of an oversized element (see XAML comment).
    private void ApplyScaleToSize()
    {
        if (_current == null) return;
        MainImage.Width = _current.Preview.PixelWidth * _currentScale;
        MainImage.Height = _current.Preview.PixelHeight * _currentScale;
        Canvas.SetLeft(MainImage, (ActualWidth - MainImage.Width) / 2);
        Canvas.SetTop(MainImage, (ActualHeight - MainImage.Height) / 2);
    }

    /// <summary>The image's current rendered rectangle, in this control's own
    /// coordinate space (includes pan, not just the base centering). Used to
    /// snap a new windowed-mode window to exactly frame the image as it
    /// currently appears.</summary>
    public Rect GetImageBoundsRelativeToControl()
    {
        if (_current == null) return Rect.Empty;
        var left = Canvas.GetLeft(MainImage) + TranslateT.X;
        var top = Canvas.GetTop(MainImage) + TranslateT.Y;
        return new Rect(left, top, MainImage.Width, MainImage.Height);
    }

    private bool IsPointOnImage(Point p)
    {
        if (_current == null) return false;
        var cx = ActualWidth / 2 + TranslateT.X;
        var cy = ActualHeight / 2 + TranslateT.Y;
        var w = MainImage.Width;
        var h = MainImage.Height;
        var left = cx - w / 2;
        var top = cy - h / 2;
        return p.X >= left && p.X <= left + w && p.Y >= top && p.Y <= top + h;
    }

    private void OnContainerResized()
    {
        if (_current == null) return;
        var wasAtFit = Math.Abs(_targetScale - _fitScale) < 0.001;
        RecalculateFit();
        if (wasAtFit || _targetScale > _fitScale)
        {
            // Either already at fit, or the container (typically the window,
            // dragged smaller) just shrank past what the current zoom needs -
            // auto-shrink so resizing the window crops the empty space around
            // the image, never the image itself. Growing the window back
            // doesn't re-expand past a deliberate zoom-out, though - only
            // shrinking auto-adjusts.
            _targetScale = _fitScale;
            StartSmoothing();
        }
        else
        {
            // Container grew (or scale is already below fit) - scale itself
            // doesn't change, but the centering offset still needs a refresh.
            ApplyScaleToSize();
        }
    }

    private void RecalculateFit()
    {
        if (_current == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        double iw = _current.Preview.PixelWidth, ih = _current.Preview.PixelHeight;
        if (iw <= 0 || ih <= 0) return;
        // Never upscale a small image to fill the screen - "fit" only shrinks
        // large images down; a small image just shows at its native size.
        _fitScale = Math.Min(1.0, Math.Min(ActualWidth / iw, ActualHeight / ih));
    }

    // ---- Continuous smoothing (replaces restart-prone Storyboard animations) --
    //
    // Re-triggering a fresh eased DoubleAnimation on every mouse-wheel tick gives
    // each animation its own acceleration curve; when ticks arrive faster than an
    // animation finishes (a normal fast scroll), every restart re-introduces the
    // curve's initial velocity spike, which reads as jerky regardless of how the
    // window itself is rendered. Instead, wheel/zoom input just updates a
    // *target* value, and a per-frame CompositionTarget.Rendering callback
    // exponentially chases the displayed value toward that target - continuous
    // by construction, however often the target changes.
    private void SetZoomTarget(double targetScale, Point anchor)
    {
        if (_current == null) return;
        targetScale = Math.Clamp(targetScale, MinScale, MaxScale);

        var oldScale = _currentScale <= 0 ? 1e-6 : _currentScale;
        var k = targetScale / oldScale;
        var containerCenter = CenterPoint();
        var oldTranslate = new Point(TranslateT.X, TranslateT.Y);

        var offsetX = anchor.X - containerCenter.X - oldTranslate.X;
        var offsetY = anchor.Y - containerCenter.Y - oldTranslate.Y;

        _targetScale = targetScale;
        _targetTranslateX = anchor.X - offsetX * k - containerCenter.X;
        _targetTranslateY = anchor.Y - offsetY * k - containerCenter.Y;

        StartSmoothing();
    }

    private void StartSmoothing()
    {
        // A completed Storyboard animation (e.g. the slide-in on TranslateT.X
        // during SetImage) keeps HOLDING its final value forever by default -
        // any later direct property set (like OnRendering's per-frame write
        // below) is then silently ignored until the animation clock is
        // explicitly cleared.
        ClearTransformAnimations();

        if (_smoothingActive) return;
        _smoothingActive = true;
        _lastTick = DateTime.UtcNow;
        CompositionTarget.Rendering += OnRendering;
    }

    private void ClearTransformAnimations()
    {
        TranslateT.BeginAnimation(TranslateTransform.XProperty, null);
        TranslateT.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private void StopSmoothing()
    {
        if (!_smoothingActive) return;
        _smoothingActive = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (dt <= 0) return;

        var t = 1 - Math.Exp(-dt / SmoothingTau);

        var newScale = Lerp(_currentScale, _targetScale, t);
        var newX = Lerp(TranslateT.X, _targetTranslateX, t);
        var newY = Lerp(TranslateT.Y, _targetTranslateY, t);

        var converged =
            Math.Abs(newScale - _targetScale) < SnapEpsilon * Math.Max(1, _targetScale) &&
            Math.Abs(newX - _targetTranslateX) < 0.05 &&
            Math.Abs(newY - _targetTranslateY) < 0.05;

        if (converged)
        {
            newScale = _targetScale;
            newX = _targetTranslateX;
            newY = _targetTranslateY;
        }

        _currentScale = newScale;
        ApplyScaleToSize();
        TranslateT.X = newX;
        TranslateT.Y = newY;

        if (converged) StopSmoothing();
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_current == null) return;
        var cursor = e.GetPosition(this);
        var factor = e.Delta > 0 ? WheelStep : 1 / WheelStep;
        SetZoomTarget(_targetScale * factor, cursor);
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (!IsPointOnImage(pos))
        {
            BackgroundClicked?.Invoke(this, EventArgs.Empty);
            return;
        }

        StopSmoothing();
        ClearTransformAnimations(); // see StartSmoothing for why this is required
        _panning = true;
        _panStartMouse = pos;
        _panStartTranslate = new Point(TranslateT.X, TranslateT.Y);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_panning) return;
        var pos = e.GetPosition(this);
        // Computed from the fixed drag-start reference (not incrementally from
        // the previous move event) so per-event rounding/coalescing can't drift
        // the two axes apart.
        var newX = _panStartTranslate.X + (pos.X - _panStartMouse.X);
        var newY = _panStartTranslate.Y + (pos.Y - _panStartMouse.Y);
        TranslateT.X = newX;
        TranslateT.Y = newY;
        _targetTranslateX = newX;
        _targetTranslateY = newY;
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _panning = false;
        ReleaseMouseCapture();
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_current == null) return;
        var pos = e.GetPosition(this);
        if (!IsPointOnImage(pos)) return;
        var target = Math.Abs(_targetScale - 1.0) < 0.02 ? _fitScale : 1.0;
        SetZoomTarget(target, pos);
    }
}
