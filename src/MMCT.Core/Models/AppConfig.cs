using System.Text.Json.Serialization;

namespace MMCT.Core.Models;

public class AppConfig
{
    [JsonPropertyName("apiBaseUrl")]
    public string ApiBaseUrl { get; set; } = "";

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "Auto";

    [JsonPropertyName("smartBatching")]
    public bool SmartBatching { get; set; } = true;

    /// <summary>
    /// Raised from 2000 to 5000 chars per batch. Larger batches significantly cut
    /// round-trip count (faster) and reduce per-batch prompt overhead tokens (cheaper).
    /// 1 char ≈ 0.6 tokens; 5000 chars ≈ 3000 content tokens - fits every supported model.
    /// </summary>
    [JsonPropertyName("maxCharsPerBatch")]
    public int MaxCharsPerBatch { get; set; } = 5000;

    /// <summary>
    /// How many mods to translate in parallel. Each mod independently uses the API,
    /// so N=4 yields ~4× wall-clock speedup on typical mod sets.
    /// </summary>
    [JsonPropertyName("concurrency")]
    public int Concurrency { get; set; } = 4;

    /// <summary>
    /// When true, the engine deduplicates identical English entries across keys within
    /// a batch, resolves them after translation, and emits minified JSON payloads.
    /// Cuts outgoing tokens by ~25-60% on real-world lang files.
    /// </summary>
    [JsonPropertyName("compactPayload")]
    public bool CompactPayload { get; set; } = true;

    [JsonPropertyName("gameVersion")]
    public string GameVersion { get; set; } = "1.20.1";

    [JsonPropertyName("packDescription")]
    public string PackDescription { get; set; } = "Auto-Translated Chinese Resource Pack by MMCT";

    [JsonPropertyName("requestTimeoutSeconds")]
    public int RequestTimeoutSeconds { get; set; } = 60;

    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>Optional CurseForge Core API key for platform verification. If empty,
    /// only Modrinth (no key required) will be queried.</summary>
    [JsonPropertyName("curseForgeApiKey")]
    public string CurseForgeApiKey { get; set; } = "";
}
