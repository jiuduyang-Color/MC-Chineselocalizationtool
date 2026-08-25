using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MMCT.Core.Models;

namespace MMCT.Core.Clients;

public class GeminiClient : IAiClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxRetries;

    public string ProviderName => "Gemini";

    public GeminiClient(string baseUrl, string apiKey, string model, int timeoutSeconds, int maxRetries)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl) ? "https://generativelanguage.googleapis.com/" : baseUrl.TrimEnd('/') + "/";
        _http = new HttpClient { BaseAddress = new Uri(url) };
        _http.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? "gemini-1.5-flash" : model;
        _maxRetries = maxRetries;
    }

    public async Task<string> TranslateAsync(List<TranslationItem> items, CancellationToken ct = default)
    {
        var sourceJson = JsonSerializer.Serialize(items.ToDictionary(i => i.Key, i => i.SourceText));
        var systemPrompt =
            "You are a professional Minecraft mod translator. Translate the following English texts to Simplified Chinese (zh_cn). " +
            "Return ONLY a valid JSON object with the same keys - no explanation, no markdown, no code fences.";

        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = sourceJson } }
                }
            },
            generationConfig = new
            {
                temperature = 0.3,
                response_mime_type = "application/json"
            }
        };

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var endpoint = $"v1beta/models/{_model}:generateContent?key={_apiKey}";
                var response = await _http.PostAsync(endpoint, content, ct);
                if (response.IsSuccessStatusCode)
                {
                    var respJson = await response.Content.ReadAsStringAsync(ct);
                    var doc = JsonDocument.Parse(respJson);
                    if (doc.RootElement.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0)
                    {
                        var parts = cands[0].GetProperty("content").GetProperty("parts");
                        if (parts.GetArrayLength() > 0)
                        {
                            var text = parts[0].GetProperty("text").GetString() ?? "";
                            return ExtractJson(text);
                        }
                    }
                }
                else if (attempt == _maxRetries)
                {
                    var errText = await response.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"API error {(int)response.StatusCode}: {errText}");
                }
            }
            catch (TaskCanceledException)
            {
                if (attempt == _maxRetries) throw new TimeoutException("Translation request timed out.");
            }
            catch when (attempt < _maxRetries) { await Task.Delay(1000 * (attempt + 1), ct); }
            catch { throw; }
        }
        throw new InvalidOperationException("Translation failed.");
    }

    private static string ExtractJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var s = text.IndexOf('\n');
            var e = text.LastIndexOf("```", StringComparison.Ordinal);
            if (s > 0 && e > s) text = text[(s + 1)..e].Trim();
        }
        var f = text.IndexOf('{');
        var l = text.LastIndexOf('}');
        if (f >= 0 && l > f) text = text[f..(l + 1)];
        return text;
    }
}
