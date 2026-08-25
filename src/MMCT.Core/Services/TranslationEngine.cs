using System.Text;
using System.Text.Json;
using MMCT.Core.Clients;
using MMCT.Core.Models;

namespace MMCT.Core.Services;

public class TranslationEngine
{
    private readonly IAiClient _client;
    private readonly int _maxCharsPerBatch;
    private readonly bool _smartBatching;
    private readonly bool _compactPayload;
    private readonly int _requestTimeoutSeconds;
    private readonly int _maxRetries;

    public TranslationEngine(IAiClient client, int maxCharsPerBatch, bool smartBatching)
        : this(client, maxCharsPerBatch, smartBatching, compactPayload: true, requestTimeoutSeconds: 60, maxRetries: 2)
    {
    }

    public TranslationEngine(IAiClient client,
                             int maxCharsPerBatch,
                             bool smartBatching,
                             bool compactPayload,
                             int requestTimeoutSeconds,
                             int maxRetries)
    {
        _client = client;
        _maxCharsPerBatch = maxCharsPerBatch > 0 ? maxCharsPerBatch : 5000;
        _smartBatching = smartBatching;
        _compactPayload = compactPayload;
        _requestTimeoutSeconds = requestTimeoutSeconds > 0 ? requestTimeoutSeconds : 60;
        _maxRetries = Math.Clamp(maxRetries, 0, 15);
    }

    public List<TranslationBatch> BuildBatches(Dictionary<string, string> sourceDict)
    {
        var items = sourceDict
            .Where(kv => !IsAlreadyTranslated(kv.Value))
            .Select(kv => new TranslationItem { Key = kv.Key, SourceText = kv.Value })
            .ToList();

        var batches = new List<TranslationBatch>();
        if (items.Count == 0) return batches;

        var threshold = _maxCharsPerBatch;
        var current = new TranslationBatch();

        foreach (var item in items)
        {
            // Compact mode: serialized bytes are shorter (no indent + potentially deduped),
            // so we can safely pack more. Conservative: still use the same chars estimate.
            var estimated = EstimateJsonChars(item) + 12;
            if (current.TotalChars + estimated > threshold && current.Items.Count > 0)
            {
                batches.Add(current);
                current = new TranslationBatch();
            }
            current.Items.Add(item);
            current.TotalChars += estimated;
        }
        if (current.Items.Count > 0)
            batches.Add(current);

        return batches;
    }

    private static bool IsAlreadyTranslated(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        int chineseCount = text.Count(c => c >= 0x4e00 && c <= 0x9fff);
        return chineseCount > text.Length / 3;
    }

    private static int EstimateJsonChars(TranslationItem item)
    {
        return item.Key.Length + item.SourceText.Length + 10;
    }

    public async Task<Dictionary<string, string>> TranslateAllAsync(
        Dictionary<string, string> sourceDict,
        IProgress<(int completedBatches, int totalBatches, int completedItems, int totalItems)>? progress = null,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(sourceDict, StringComparer.Ordinal);
        var batches = BuildBatches(sourceDict);

        if (batches.Count == 0) return result;

        var totalItems = batches.Sum(b => b.Items.Count);
        var completedItems = 0;

        for (var i = 0; i < batches.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var batch = batches[i];
            var translatedDict = await TranslateBatchWithRetry(batch, ct).ConfigureAwait(false);

            foreach (var item in batch.Items)
            {
                if (translatedDict.TryGetValue(item.Key, out var t) && !string.IsNullOrWhiteSpace(t))
                    result[item.Key] = t;
                else
                    result[item.Key] = item.SourceText;
            }

            completedItems += batch.Items.Count;
            progress?.Report((i + 1, batches.Count, completedItems, totalItems));
        }
        return result;
    }

    private async Task<Dictionary<string, string>> TranslateBatchWithRetry(TranslationBatch batch, CancellationToken ct)
    {
        Exception? last = null;
        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var payload = BuildPayload(batch.Items, out var dedupPlan);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _requestTimeoutSeconds) * 4L / 3));

                var translatedJson = await _client.TranslateAsync(payload, cts.Token).ConfigureAwait(false);
                var parsed = ParseTranslationResult(translatedJson);
                return RestoreDedup(parsed, dedupPlan, batch.Items);
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                last = ex;
                var delayMs = 500 * (int)Math.Pow(2, attempt);
                await Task.Delay(Math.Clamp(delayMs, 100, 5000), ct).ConfigureAwait(false);
            }
        }
        throw last ?? new InvalidOperationException("Translation batch failed.");
    }

    /// <summary>Token-optimized payload generator. Returns a minimal (no-indent) JSON and
    /// optionally deduplicates identical English strings to reduce outgoing tokens.</summary>
    private List<TranslationItem> BuildPayload(List<TranslationItem> items, out DedupPlan plan)
    {
        plan = new DedupPlan();
        if (!_compactPayload)
            return items;

        // Step 1: deduplicate by source text. Assign each distinct text an _index key.
        var byText = new Dictionary<string, string>(StringComparer.Ordinal);
        var forward = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!byText.ContainsKey(item.SourceText))
            {
                var idxKey = "__d" + byText.Count.ToString("D4");
                byText[item.SourceText] = idxKey;
                forward[idxKey] = new List<string>();
            }
            forward[byText[item.SourceText]].Add(item.Key);
        }

        // Only apply dedup if it actually saves space (at least 3 duplicated rows).
        var dupCount = items.Count - forward.Count;
        if (dupCount < 3)
            return items;

        plan.Applied = true;
        plan.IndexKeyToOriginalKeys = forward;

        var compact = new List<TranslationItem>(forward.Count);
        foreach (var kv in forward)
        {
            // find one sample sourceText for the index key
            var sample = items.First(i => i.Key == kv.Value[0]).SourceText;
            compact.Add(new TranslationItem { Key = kv.Key, SourceText = sample });
        }
        return compact;
    }

    private static Dictionary<string, string> RestoreDedup(Dictionary<string, string> parsed, DedupPlan plan,
                                                           List<TranslationItem> originals)
    {
        if (!plan.Applied)
            return parsed;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in originals)
            result[row.Key] = row.SourceText;

        foreach (var (idxKey, origKeys) in plan.IndexKeyToOriginalKeys)
        {
            // Fallback 1: indexed key. Fallback 2: one of the original keys happens to match.
            if (!parsed.TryGetValue(idxKey, out var translated))
            {
                foreach (var ok in origKeys)
                {
                    if (parsed.TryGetValue(ok, out translated)) break;
                }
            }
            // If the AI omitted this distinct key entirely, do NOT leak a neighbouring translation.
            // Fall back to pre-filled source English, which keeps the output valid for MC's lang loader.
            if (string.IsNullOrWhiteSpace(translated)) continue;
            foreach (var ok in origKeys)
                result[ok] = translated;
        }
        return result;
    }

    private static Dictionary<string, string> ParseTranslationResult(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    result[prop.Name] = prop.Value.GetString() ?? "";
            }
        }
        catch
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(
                json, @"\""([^\""]+)\""\s*:\s*\""((?:[^\""\\]|\\.)*)\""");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (m.Groups.Count >= 3)
                    result[m.Groups[1].Value] = System.Text.RegularExpressions.Regex.Unescape(m.Groups[2].Value);
            }
        }
        return result;
    }

    private sealed class DedupPlan
    {
        public bool Applied;
        public Dictionary<string, List<string>> IndexKeyToOriginalKeys = new(StringComparer.Ordinal);
    }
}

/// <summary>Minimal JSON writer helpers (kept for future Payload() expansion via WriteRaw).</summary>
internal static class JsonMini
{
    public static string StringifyCompact(IEnumerable<TranslationItem> items)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        bool first = true;
        foreach (var it in items)
        {
            if (!first) sb.Append(',');
            first = false;
            AppendEscaped(sb, it.Key);
            sb.Append(':');
            AppendEscaped(sb, it.SourceText);
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendEscaped(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20)
                        sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    else
                        sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
    }
}
