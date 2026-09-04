using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Viewer;

public partial class RenameWindow : Window
{
    private readonly string _directory;
    private readonly string _extension;
    private readonly string _originalPath;

    public string? NewPath { get; private set; }

    public RenameWindow(string currentPath)
    {
        InitializeComponent();
        _originalPath = currentPath;
        _directory = Path.GetDirectoryName(currentPath) ?? "";
        _extension = Path.GetExtension(currentPath);
        NameBox.Text = Path.GetFileNameWithoutExtension(currentPath);
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Ok_Click(sender, e);
        else if (e.Key == Key.Escape) Cancel_Click(sender, e);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ErrorText.Text = "Имя не может быть пустым.";
            return;
        }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ErrorText.Text = "Имя содержит недопустимые символы.";
            return;
        }

        var candidate = Path.Combine(_directory, name + _extension);
        if (!string.Equals(candidate, _originalPath, System.StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
        {
            ErrorText.Text = "Файл с таким именем уже существует.";
            return;
        }

        NewPath = candidate;
        DialogResult = true;
        Close();
    }
}
