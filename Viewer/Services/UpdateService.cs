using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Viewer.Services;

// Checks GitHub Releases for a newer tagged version than the one currently
// running, and can carry out the update itself: download the release's zip
// asset into an isolated temp folder, then hand off to a small PowerShell
// script that waits for this process to exit, mirrors the temp folder over
// the install directory, deletes the temp folder, and relaunches the app.
// Doing the actual file swap from a separate script (not from inside the
// running app) is required - a running .exe can't overwrite itself.
public static class UpdateService
{
    private const string Owner = "AmajaWeed";
    private const string Repo = "BananaView";

    public sealed record UpdateCheckResult(bool UpdateAvailable, string? LatestVersion, string? ReleaseUrl, string? AssetUrl, string? Error);

    public static async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        try
        {
            using var http = NewClient();

            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return new UpdateCheckResult(false, null, null, null, $"Сервер вернул {(int)response.StatusCode}");

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var releaseUrl = doc.RootElement.TryGetProperty("html_url", out var htmlUrlProp)
                ? htmlUrlProp.GetString()
                : $"https://github.com/{Owner}/{Repo}/releases/latest";

            string? assetUrl = null;
            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        assetUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            var tagVersionText = tag.TrimStart('v', 'V');
            if (!Version.TryParse(tagVersionText, out var latest))
                return new UpdateCheckResult(false, tag, releaseUrl, assetUrl, "Не удалось разобрать версию релиза");

            var isNewer = latest > CurrentVersion;
            return new UpdateCheckResult(isNewer, tag, releaseUrl, assetUrl, null);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, null, null, null, ex.Message);
        }
    }

    // Downloads the release zip into %TEMP%\BananaViewUpdate\<version>\download\
    // and extracts it into ...\extracted\ next to it - both under one temp
    // root so ApplyUpdateAndRestart can delete the whole thing in one step
    // once the swap is done.
    public static async Task<string> DownloadAndExtractAsync(string assetUrl, string version, IProgress<string>? progress = null)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "BananaViewUpdate", version.TrimStart('v', 'V'));
        if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        Directory.CreateDirectory(tempRoot);

        var zipPath = Path.Combine(tempRoot, "update.zip");
        var extractedDir = Path.Combine(tempRoot, "extracted");

        progress?.Report("Загрузка обновления...");
        using (var http = NewClient())
        {
            http.Timeout = TimeSpan.FromMinutes(5);
            using var response = await http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var fileStream = File.Create(zipPath);
            await response.Content.CopyToAsync(fileStream);
        }

        progress?.Report("Распаковка...");
        Directory.CreateDirectory(extractedDir);
        ZipFile.ExtractToDirectory(zipPath, extractedDir);

        // A zip made from "right-click -> Compress" on the publish folder
        // nests everything under one subfolder - if that's all extractedDir
        // contains, treat that subfolder as the real payload root.
        var entries = Directory.GetFileSystemEntries(extractedDir);
        if (entries.Length == 1 && Directory.Exists(entries[0]))
            extractedDir = entries[0];

        return extractedDir;
    }

    // Writes and launches a PowerShell script that waits for this process to
    // exit, mirrors extractedDir over the install directory (robocopy /MIR),
    // deletes the whole temp update root, and starts the app again - then
    // shuts this process down so the script's wait immediately succeeds.
    //
    // The install directory is Program Files (the installer requires admin
    // to put it there), but BananaView.exe itself runs unelevated on a
    // normal launch - so the swap script needs its OWN elevation request
    // (Verb=runas) or robocopy silently fails to write there, the "update"
    // does nothing, and relaunching just brings back the same old version.
    // That silent failure - no error, no visible sign anything went wrong -
    // is exactly what shipped originally; robocopy's own output is now
    // logged instead of discarded so a future failure is at least visible.
    public static void ApplyUpdateAndRestart(string extractedDir)
    {
        var installDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var exePath = Path.Combine(installDir, "BananaView.exe");
        var tempRoot = Directory.GetParent(extractedDir)!.FullName; // .../BananaViewUpdate/<version>
        var scriptPath = Path.Combine(Path.GetTempPath(), $"BananaViewUpdate_{Guid.NewGuid():N}.ps1");
        var logPath = Path.Combine(Path.GetTempPath(), "BananaViewUpdate.log");

        var script = $$"""
            $ErrorActionPreference = 'SilentlyContinue'
            $exe = "{{exePath}}"
            $log = "{{logPath}}"
            for ($i = 0; $i -lt 60; $i++) {
                $locked = $true
                try {
                    $stream = [IO.File]::Open($exe, 'Open', 'ReadWrite', 'None')
                    $stream.Close()
                    $locked = $false
                } catch {}
                if (-not $locked) { break }
                Start-Sleep -Milliseconds 500
            }
            robocopy "{{extractedDir}}" "{{installDir}}" /MIR /R:3 /W:1 *>> $log
            "robocopy exit code: $LASTEXITCODE" | Out-File -Append $log
            Remove-Item -Recurse -Force "{{tempRoot}}"
            Start-Process -FilePath "{{exePath}}"
            """;
        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo("powershell.exe",
            $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Verb = "runas", // triggers the UAC prompt this needed but never asked for
        });

        System.Windows.Application.Current.Shutdown();
    }

    private static HttpClient NewClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BananaView", CurrentVersion.ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.Timeout = TimeSpan.FromSeconds(10);
        return http;
    }

    public static Version CurrentVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
}
