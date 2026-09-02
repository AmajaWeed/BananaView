using System;
using System.Windows;
using System.Windows.Input;
using Viewer.Services;

namespace Viewer;

public partial class UpdateWindow : Window
{
    private readonly UpdateService.UpdateCheckResult _result;

    public UpdateWindow(UpdateService.UpdateCheckResult result)
    {
        InitializeComponent();
        _result = result;
        VersionsText.Text = $"Установлена {UpdateService.CurrentVersion}  ->  доступна {result.LatestVersion}";
        UpdateButton.IsEnabled = result.AssetUrl != null;
        if (result.AssetUrl == null)
            StatusText.Text = "У релиза нет файла для автообновления - откройте страницу загрузки вручную.";
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.SkippedUpdateVersion = _result.LatestVersion;
        AppSettings.Current.Save();
        Close();
    }

    private void Postpone_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.UpdatePostponedUntilUtc = DateTime.UtcNow.AddDays(1);
        AppSettings.Current.Save();
        Close();
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_result.AssetUrl == null || _result.LatestVersion == null) return;

        UpdateButton.IsEnabled = false;
        SkipButton.IsEnabled = false;
        PostponeButton.IsEnabled = false;
        ProgressIndicator.Visibility = Visibility.Visible;

        try
        {
            var progress = new Progress<string>(msg => StatusText.Text = msg);
            var extractedDir = await UpdateService.DownloadAndExtractAsync(_result.AssetUrl, _result.LatestVersion, progress);
            StatusText.Text = "Перезапуск...";
            // Shuts the whole app down as part of handing off to the swap script.
            UpdateService.ApplyUpdateAndRestart(extractedDir);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Не удалось обновить: {ex.Message}";
            ProgressIndicator.Visibility = Visibility.Collapsed;
            UpdateButton.IsEnabled = true;
            SkipButton.IsEnabled = true;
            PostponeButton.IsEnabled = true;
        }
    }
}
