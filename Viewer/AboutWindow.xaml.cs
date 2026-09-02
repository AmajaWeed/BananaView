using System.Windows;
using System.Windows.Input;
using Viewer.Services;

namespace Viewer;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Версия {UpdateService.CurrentVersion}";
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Проверка обновлений...";

        var result = await UpdateService.CheckForUpdateAsync();

        if (result.Error != null)
        {
            UpdateStatusText.Text = $"Не удалось проверить обновления: {result.Error}";
        }
        else if (result.UpdateAvailable)
        {
            UpdateStatusText.Text = "";
            new UpdateWindow(result) { Owner = this }.ShowDialog();
        }
        else
        {
            UpdateStatusText.Text = "У вас установлена последняя версия.";
        }

        CheckUpdateButton.IsEnabled = true;
    }
}
