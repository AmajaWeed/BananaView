using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace Viewer.ViewModels;

public sealed class FilmstripItem : INotifyPropertyChanged
{
    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);

    private BitmapSource? _thumbnail;
    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; OnPropertyChanged(); }
    }

    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set { _isCurrent = value; OnPropertyChanged(); }
    }

    public FilmstripItem(string path) => Path = path;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
