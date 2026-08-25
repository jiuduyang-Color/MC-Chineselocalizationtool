using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MMCT.Core.Models;

namespace MMCT.Core.Clients;

/// <summary>
/// OpenAI-compatible API client (DeepSeek, OpenAI, etc.).
/// NOTE: Retry logic is handled by TranslationEngine.TranslateBatchWithRetry.
/// This client does a single attempt and throws on failure.
/// </summary>
public class OpenAiCompatibleClient : IAiClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public string ProviderName { get; }

    public OpenAiCompatibleClient(string baseUrl, string apiKey, string model,
        string providerName, int timeoutSeconds, int maxRetries)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _http.Timeout = TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 120);
        _apiKey = apiKey;
        _model = model;
        ProviderName = providerName;
    }

    public async Task<string> TranslateAsync(List<TranslationItem> items, CancellationToken ct = default)
    {
        var sourceJson = JsonSerializer.Serialize(items.ToDictionary(i => i.Key, i => i.SourceText),
            new JsonSerializerOptions { WriteIndented = false });

        var systemPrompt =
            "You are a professional Minecraft mod translator. Translate the following English texts to Simplified Chinese (zh_cn). " +
            "Rules:\n" +
            "1. Preserve all Minecraft terminology (e.g., item names, entity names, block names) accurately.\n" +
            "2. Use standard Chinese translation conventions for Minecraft gameplay terms.\n" +
            "3. Keep the original JSON key structure intact - return ONLY a valid JSON object with the same keys and translated string values.\n" +
            "4. Do not add any explanation, markdown code fences, or extra text - output ONLY the JSON.\n" +
            "5. Keep color codes (e.g., §6, §a), formatting symbols, placeholders (e.g., %s, %1$s, {0}) and control characters exactly as they appear.\n" +
            "6. Ensure natural and fluent Chinese that fits Minecraft's translation style.";

        var messages = new[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = sourceJson }
        };

        var payload = new
        {
            model = _model,
            messages,
            temperature = 0.3,
            response_format = new { type = "json_object" }
        };

        // Single attempt - retry is handled by TranslationEngine
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("chat/completions", content, ct);

        if (response.IsSuccessStatusCode)
        {
            var respJson = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(respJson);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var msg = choices[0].GetProperty("message");
                var text = msg.GetProperty("content").GetString() ?? "";
                return ExtractJson(text);
            }
            throw new InvalidOperationException("API returned no choices.");
        }
        else
        {
            var errText = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"API error {(int)response.StatusCode}: {errText}");
        }
    }

    private static string ExtractJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var start = text.IndexOf('\n');
            var end = text.LastIndexOf("```", StringComparison.Ordinal);
            if (start > 0 && end > start)
                text = text[(start + 1)..end].Trim();
        }
        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            text = text[firstBrace..(lastBrace + 1)];
        return text;
    }
}
