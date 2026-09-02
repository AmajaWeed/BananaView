using System;
using System.IO;
using System.Text.Json;

namespace Viewer.Services;

// Persisted app preferences. Deliberately tiny - only the handful of things
// that are actually user-configurable today - rather than a generic settings
// framework nothing else needs yet.
public sealed class AppSettings
{
    private static readonly string SettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BananaView", "settings.json");

    public int ThumbnailSize { get; set; } = 100;

    // "Пропустить версию" - never prompt about this specific version again.
    public string? SkippedUpdateVersion { get; set; }

    // "Отложить" - don't prompt again until this time passes (still checkable manually in Settings).
    public DateTime? UpdatePostponedUntilUtc { get; set; }

    public static AppSettings Current { get; private set; } = Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Corrupt/unreadable settings file - fall back to defaults rather than crash on startup.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Best-effort - a failed save just means the setting doesn't survive restart.
        }
    }
}
