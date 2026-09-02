using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Viewer.Services;

// Checks GitHub Releases for a newer tagged version than the one currently
// running. Deliberately does NOT download or run anything itself - it only
// reports whether an update exists and hands back the release page URL, so
// the actual download/install stays a manual, user-confirmed action (opened
// in the default browser).
public static class UpdateService
{
    private const string Owner = "AmajaWeed";
    private const string Repo = "BananaView";

    public sealed record UpdateCheckResult(bool UpdateAvailable, string? LatestVersion, string? ReleaseUrl, string? Error);

    public static async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BananaView", CurrentVersion.ToString()));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            http.Timeout = TimeSpan.FromSeconds(10);

            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return new UpdateCheckResult(false, null, null, $"Сервер вернул {(int)response.StatusCode}");

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var releaseUrl = doc.RootElement.TryGetProperty("html_url", out var htmlUrlProp)
                ? htmlUrlProp.GetString()
                : $"https://github.com/{Owner}/{Repo}/releases/latest";

            var tagVersionText = tag.TrimStart('v', 'V');
            if (!Version.TryParse(tagVersionText, out var latest))
                return new UpdateCheckResult(false, tag, releaseUrl, "Не удалось разобрать версию релиза");

            var isNewer = latest > CurrentVersion;
            return new UpdateCheckResult(isNewer, tag, releaseUrl, null);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, null, null, ex.Message);
        }
    }

    public static Version CurrentVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
}
