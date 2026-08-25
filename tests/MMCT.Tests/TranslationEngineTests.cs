using MMCT.Core.Clients;
using MMCT.Core.Models;
using MMCT.Core.Services;

namespace MMCT.Tests;

public class TranslationEngineTests
{
    private class StubAiClient : IAiClient
    {
        public string ProviderName => "Stub";

        public int CallCount;
        public List<List<TranslationItem>> ReceivedBatches = new();

        public Task<string> TranslateAsync(List<TranslationItem> items, CancellationToken ct = default)
        {
            CallCount++;
            ReceivedBatches.Add(items);
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var it in items)
                dict[it.Key] = "ZH_" + it.SourceText;
            return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(dict));
        }
    }

    [Fact]
    public void BuildBatches_RespectsMaxCharsPerBatch()
    {
        var client = new StubAiClient();
        var engine = new TranslationEngine(client, maxCharsPerBatch: 100, smartBatching: true);

        var src = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < 10; i++)
            src["k" + i] = new string('x', 50);

        var batches = engine.BuildBatches(src);
        Assert.True(batches.Count >= 3);
        Assert.All(batches, b => Assert.True(b.Items.Count > 0));
    }

    [Fact]
    public void BuildBatches_SkipsAlreadyChineseEntries()
    {
        var client = new StubAiClient();
        var engine = new TranslationEngine(client, 5000, true);
        var src = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "k1", "Hello there" },
            { "k2", "这是已经是中文的条目啦，应该跳过哦" },
            { "k3", "Another English one" }
        };
        var batches = engine.BuildBatches(src);
        var allKeys = batches.SelectMany(b => b.Items.Select(i => i.Key)).ToList();
        Assert.DoesNotContain("k2", allKeys);
        Assert.Contains("k1", allKeys);
        Assert.Contains("k3", allKeys);
    }

    [Fact]
    public async Task TranslateAllAsync_CallsClientAndMergesResults()
    {
        var client = new StubAiClient();
        var engine = new TranslationEngine(client, 5000, true);
        var src = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "a.b", "Hello" },
            { "a.c", "World" }
        };
        var result = await engine.TranslateAllAsync(src);

        Assert.Equal(1, client.CallCount);
        Assert.Equal("ZH_Hello", result["a.b"]);
        Assert.Equal("ZH_World", result["a.c"]);
    }

    [Fact]
    public async Task TranslateAllAsync_ReportsProgress()
    {
        var client = new StubAiClient();
        var engine = new TranslationEngine(client, 100, true);

        var src = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < 10; i++)
            src["k" + i] = new string('x', 50);

        var reports = new List<(int cb, int tb, int ci, int ti)>();
        // Use inline IProgress to avoid Progress<T>'s SyncContext dispatch on the thread pool.
        var prog = new InlineProgress<(int, int, int, int)>(p => reports.Add((p.Item1, p.Item2, p.Item3, p.Item4)));

        await engine.TranslateAllAsync(src, prog);

        Assert.NotEmpty(reports);
        Assert.All(reports, r =>
        {
            Assert.True(r.cb <= r.tb);
            Assert.True(r.ci <= r.ti);
        });
        var last = reports.Last();
        Assert.Equal(last.tb, last.cb);
        Assert.Equal(last.ti, last.ci);
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public InlineProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }

    [Fact]
    public async Task TranslateAllAsync_Cancellation_StopsQuickly()
    {
        var slow = new SlowClient();
        var engine = new TranslationEngine(slow, 5000, true);
        var src = new Dictionary<string, string>(StringComparer.Ordinal) { { "k", "V" } };
        using var cts = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.TranslateAllAsync(src, null, cts.Token));
    }

    private class SlowClient : IAiClient
    {
        public string ProviderName => "Slow";
        public async Task<string> TranslateAsync(List<TranslationItem> items, CancellationToken ct = default)
        {
            await Task.Delay(5000, ct);
            return "{}";
        }
    }

    // -------------------- Compact / Dedup quality regression tests --------------------

    [Fact]
    public async Task CompactPayload_DedupRoundTrip_ResultDictKeysEqualToSource()
    {
        // 9 rows, 3 distinct source texts (3 repetitions each). DupCount = 6 >= 3 → dedup triggers.
        var client = new StubAiClient();
        var engine = new TranslationEngine(client, 10000, smartBatching: true,
            compactPayload: true, requestTimeoutSeconds: 5, maxRetries: 0);

        var src = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["modA.ui.accept"]       = "Accept",
            ["modA.ui.ok_button"]    = "Accept",
            ["modA.ui.do_accept"]    = "Accept",
            ["modA.ui.cancel"]       = "Cancel",
            ["modA.ui.cancel_btn"]   = "Cancel",
            ["modA.ui.cancel_dialog"]= "Cancel",
            ["modA.ui.next"]         = "Next Page",
            ["modA.ui.next_page"]    = "Next Page",
            ["modA.ui.go_next"]      = "Next Page",
        };
        var result = await engine.TranslateAllAsync(src);

        // KEY CONTRACT: finished zh_cn dict MUST have exactly the same key set as source.
        Assert.Equal(src.Keys.OrderBy(k => k), result.Keys.OrderBy(k => k));

        // Every key has a translation.
        Assert.All(result.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));

        // All original "Accept" aliases share the SAME translated value (not jumbled).
        Assert.Equal(result["modA.ui.accept"], result["modA.ui.ok_button"]);
        Assert.Equal(result["modA.ui.accept"], result["modA.ui.do_accept"]);
        Assert.Equal("ZH_Accept", result["modA.ui.accept"]);

        // All original "Cancel" aliases likewise.
        Assert.Equal("ZH_Cancel", result["modA.ui.cancel"]);
        Assert.Equal(result["modA.ui.cancel"], result["modA.ui.cancel_btn"]);
        Assert.Equal(result["modA.ui.cancel"], result["modA.ui.cancel_dialog"]);

        // All original "Next Page" aliases.
        Assert.Equal("ZH_Next Page", result["modA.ui.next"]);
        Assert.Equal(result["modA.ui.next"], result["modA.ui.next_page"]);
        Assert.Equal(result["modA.ui.next"], result["modA.ui.go_next"]);
    }

    [Fact]
    public async Task CompactPayload_Off_AllNineSentToAi_NoDedupFallbackRisk()
    {
        var client = new StubAiClient();
        var engine = new TranslationEngine(client, 10000, smartBatching: true,
            compactPayload: false, requestTimeoutSeconds: 5, maxRetries: 0);

        var src = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["k1"] = "A", ["k2"] = "A", ["k3"] = "A",
            ["k4"] = "B", ["k5"] = "B", ["k6"] = "B",
            ["k7"] = "C", ["k8"] = "C", ["k9"] = "C",
        };
        var result = await engine.TranslateAllAsync(src);

        // No dedup: StubAiClient saw 9 items in one batch.
        var batch = Assert.Single(client.ReceivedBatches);
        Assert.Equal(9, batch.Count);

        // Keys still preserved end-to-end.
        Assert.Equal(src.Keys.OrderBy(k => k), result.Keys.OrderBy(k => k));
        Assert.Equal("ZH_A", result["k1"]);
        Assert.Equal("ZH_B", result["k4"]);
        Assert.Equal("ZH_C", result["k7"]);
    }

    [Fact]
    public async Task CompactPayload_AiOmitsOneIndexKey_FallsBackToEnglishSource_NoWrongValueLeak()
    {
        // AI returns translation for only one distinct value. Missing ones stay as English.
        var client = new PartialReturnClient(returnForSourceText: "Accept");
        var engine = new TranslationEngine(client, 10000, smartBatching: true,
            compactPayload: true, requestTimeoutSeconds: 5, maxRetries: 0);

        var src = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a1"] = "Accept", ["a2"] = "Accept", ["a3"] = "Accept",
            ["b1"] = "Cancel", ["b2"] = "Cancel", ["b3"] = "Cancel",
        };
        var result = await engine.TranslateAllAsync(src);

        // Accept group translated.
        Assert.Equal("OK_zh", result["a1"]);
        Assert.Equal("OK_zh", result["a2"]);
        Assert.Equal("OK_zh", result["a3"]);

        // Cancel group: AI omitted → MUST fall back to source English, never copy a neighbour translation.
        Assert.Equal("Cancel", result["b1"]);
        Assert.Equal("Cancel", result["b2"]);
        Assert.Equal("Cancel", result["b3"]);
    }

    private sealed class PartialReturnClient : IAiClient
    {
        private readonly string _returnFor;
        public PartialReturnClient(string returnForSourceText) => _returnFor = returnForSourceText;
        public string ProviderName => "Partial";
        public Task<string> TranslateAsync(List<TranslationItem> items, CancellationToken ct = default)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var it in items)
            {
                if (it.SourceText == _returnFor)
                    dict[it.Key] = "OK_zh";
            }
            return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(dict));
        }
    }
}
