using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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
            UpdateStatusText.Text = $"Доступна новая версия {result.LatestVersion}.";
            var openButton = new Button
            {
                Content = "Открыть страницу загрузки",
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 8, 0, 0),
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            openButton.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(result.ReleaseUrl!) { UseShellExecute = true }); }
                catch { /* no default browser handler - not fatal */ }
            };
            UpdateStatusPanel.Children.Add(openButton);
        }
        else
        {
            UpdateStatusText.Text = "У вас установлена последняя версия.";
        }

        CheckUpdateButton.IsEnabled = true;
    }
}
