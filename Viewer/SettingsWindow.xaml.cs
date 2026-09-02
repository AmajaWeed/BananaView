using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Viewer.Services;

namespace Viewer;

public partial class SettingsWindow : Window
{
    private static readonly string DiskCacheDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BananaView", "ThumbnailCache");

    private readonly ThumbnailCache _thumbCache;
    private bool _loaded;

    // Raised when a setting changes that the filmstrip needs to react to
    // (currently just thumbnail size) - MainWindow subscribes and re-renders.
    public event EventHandler? ThumbnailSettingsChanged;

    public SettingsWindow(ThumbnailCache thumbCache)
    {
        InitializeComponent();
        _thumbCache = thumbCache;

        ThumbnailSizeSlider.Value = AppSettings.Current.ThumbnailSize;
        ThumbnailSizeLabel.Text = $"{AppSettings.Current.ThumbnailSize} px";
        VersionText.Text = $"Версия {UpdateService.CurrentVersion}";
        UpdateCacheStatus();
        _loaded = true;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ThumbnailSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var size = (int)e.NewValue;
        ThumbnailSizeLabel.Text = $"{size} px";
        if (!_loaded) return;

        AppSettings.Current.ThumbnailSize = size;
        AppSettings.Current.Save();
        _thumbCache.SetThumbnailSize(size);
        ThumbnailSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Directory.Exists(DiskCacheDir))
            {
                foreach (var file in Directory.GetFiles(DiskCacheDir))
                {
                    try { File.Delete(file); } catch { /* skip files in use */ }
                }
            }
            _thumbCache.SetThumbnailSize(AppSettings.Current.ThumbnailSize); // forces a memory-cache clear too
            ThumbnailSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch { /* best-effort */ }
        UpdateCacheStatus();
    }

    private void UpdateCacheStatus()
    {
        try
        {
            var count = Directory.Exists(DiskCacheDir) ? Directory.GetFiles(DiskCacheDir).Length : 0;
            CacheStatusText.Text = $"Закэшировано миниатюр: {count}";
        }
        catch
        {
            CacheStatusText.Text = "";
        }
    }

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
