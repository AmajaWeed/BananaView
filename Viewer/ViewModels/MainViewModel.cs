using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Viewer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<FilmstripItem> Items { get; } = new();

    private int _currentIndex = -1;
    public int CurrentIndex
    {
        get => _currentIndex;
        set { _currentIndex = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
