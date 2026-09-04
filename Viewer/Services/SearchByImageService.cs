using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Viewer.Services;

// Uploads the image directly to Google's / Yandex's own reverse-image-search
// upload endpoints and returns the results page URL - the same requests
// their respective web UIs make, reverse-engineered from the dessant/
// search-by-image browser extension's source (src/engines/engines.js,
// src/engines/yandex.js). This replaces an earlier clipboard-paste-based
// approach that depended on the target site accepting a pasted image in its
// search box, which proved unreliable in practice; a direct upload has no
// such dependency and lands the user straight on results.
public static class SearchByImageService
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public static async Task<string> SearchGoogleAsync(byte[] jpegBytes)
    {
        using var http = NewClient();

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(jpegBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(imageContent, "encoded_image", "image.jpg");
        content.Add(new StringContent(""), "image_url");
        content.Add(new StringContent("Google Chrome 120.0.0.0 (Official) Windows"), "sbisrc");

        using var response = await http.PostAsync("https://www.google.com/searchbyimage/upload", content);
        response.EnsureSuccessStatusCode();

        return response.RequestMessage?.RequestUri?.ToString()
            ?? throw new InvalidOperationException("Google did not return a results URL.");
    }

    public static async Task<string> SearchYandexAsync(byte[] jpegBytes)
    {
        using var http = NewClient();

        var url = "https://yandex.ru/images/touch/search?rpt=imageview&format=json&request=" +
            Uri.EscapeDataString("{\"blocks\":[{\"block\":\"cbir-uploader__get-cbir-id\"}]}");

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(jpegBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(imageContent, "upfile", "image.jpg");

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var p = doc.RootElement.GetProperty("blocks")[0].GetProperty("params");
        var cbirId = p.GetProperty("cbirId").GetString();
        var originalImageUrl = p.GetProperty("originalImageUrl").GetString();

        return $"https://yandex.ru/images/search?cbir_id={cbirId}&rpt=imageview&tabInt=1&url={Uri.EscapeDataString(originalImageUrl!)}";
    }

    private static HttpClient NewClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(DesktopUserAgent);
        return http;
    }
}
