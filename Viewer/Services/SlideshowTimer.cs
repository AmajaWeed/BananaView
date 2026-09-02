using System;
using System.Windows.Threading;

namespace Viewer.Services;

public sealed class SlideshowTimer
{
    private readonly DispatcherTimer _timer;

    public event Action? Tick;
    public bool IsRunning { get; private set; }

    public SlideshowTimer(TimeSpan? interval = null)
    {
        _timer = new DispatcherTimer { Interval = interval ?? TimeSpan.FromSeconds(4) };
        _timer.Tick += (_, _) => Tick?.Invoke();
    }

    public void Start()
    {
        _timer.Start();
        IsRunning = true;
    }

    public void Stop()
    {
        _timer.Stop();
        IsRunning = false;
    }

    public void Toggle()
    {
        if (IsRunning) Stop(); else Start();
    }
}
