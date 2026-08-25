using MMCT.Core.Models;

namespace MMCT.Core.Clients;

public static class AiClientFactory
{
    public enum ProviderType
    {
        Auto, OpenAI, DeepSeek, Claude, Gemini
    }

    public static ProviderType DetectProvider(string baseUrl, string explicitProvider)
    {
        if (!string.IsNullOrWhiteSpace(explicitProvider) && Enum.TryParse<ProviderType>(explicitProvider, true, out var p))
        {
            if (p != ProviderType.Auto) return p;
        }

        var url = (baseUrl ?? "").ToLowerInvariant();
        if (url.Contains("deepseek")) return ProviderType.DeepSeek;
        if (url.Contains("anthropic") || url.Contains("claude")) return ProviderType.Claude;
        if (url.Contains("google") || url.Contains("generativelanguage") || url.Contains("gemini")) return ProviderType.Gemini;
        if (url.Contains("openai") || url.Contains("api.openai.com")) return ProviderType.OpenAI;
        return ProviderType.OpenAI;
    }

    public static IAiClient Create(AppConfig config)
    {
        var provider = DetectProvider(config.ApiBaseUrl, config.Provider);
        var timeout = config.RequestTimeoutSeconds > 0 ? config.RequestTimeoutSeconds : 60;
        var retries = config.MaxRetries >= 0 ? config.MaxRetries : 3;

        return provider switch
        {
            ProviderType.DeepSeek => new OpenAiCompatibleClient(
                string.IsNullOrWhiteSpace(config.ApiBaseUrl) ? "https://api.deepseek.com/v1" : config.ApiBaseUrl,
                config.ApiKey,
                string.IsNullOrWhiteSpace(config.Model) ? "deepseek-chat" : config.Model,
                "DeepSeek", timeout, retries),
            ProviderType.Claude => new ClaudeClient(config.ApiBaseUrl, config.ApiKey, config.Model, timeout, retries),
            ProviderType.Gemini => new GeminiClient(config.ApiBaseUrl, config.ApiKey, config.Model, timeout, retries),
            _ => new OpenAiCompatibleClient(
                string.IsNullOrWhiteSpace(config.ApiBaseUrl) ? "https://api.openai.com/v1" : config.ApiBaseUrl,
                config.ApiKey,
                string.IsNullOrWhiteSpace(config.Model) ? "gpt-4o-mini" : config.Model,
                provider == ProviderType.OpenAI ? "OpenAI" : "OpenAI-Compatible", timeout, retries)
        };
    }

    public static ProviderType[] GetAllProviders() =>
        new[] { ProviderType.Auto, ProviderType.OpenAI, ProviderType.DeepSeek, ProviderType.Claude, ProviderType.Gemini };
}
