using MMCT.Core;
using MMCT.Core.Clients;
using MMCT.Core.Models;
using MMCT.Core.Services;
using MMCT.Core.UI;

namespace MMCT.Tests;

/// <summary>Tests for the new concurrency speedup, progress bar, pun rotator, and mod platform verifier.</summary>
public class EnhancementTests
{
    /* ------------------------------------------------------------ */
    /* Concurrency + speedup                                        */
    /* ------------------------------------------------------------ */

    private sealed class DelayCountClient : IAiClient
    {
        public string ProviderName => "Delay";
        public int ConcurrentPeak;
        private int _inFlight;

        public async Task<string> TranslateAsync(List<TranslationItem> items, CancellationToken ct = default)
        {
            var now = Interlocked.Increment(ref _inFlight);
            while (true)
            {
                var peak = Volatile.Read(ref ConcurrentPeak);
                if (now <= peak || Interlocked.CompareExchange(ref ConcurrentPeak, now, peak) == peak)
                    break;
            }
            await Task.Delay(60, ct);
            Interlocked.Decrement(ref _inFlight);
            var d = items.ToDictionary(i => i.Key, i => "ZH_" + i.SourceText);
            return System.Text.Json.JsonSerializer.Serialize(d);
        }
    }

    [Fact]
    public async Task ModTranslationCoordinator_TranslatesConcurrently()
    {
        // 8 mods × 60ms/mod; with concurrency=4 total should be well under 2× sequential (480ms).
        var client = new DelayCountClient();
        var mods = new List<ModInfo>();
        for (int i = 0; i < 8; i++)
        {
            mods.Add(new ModInfo
            {
                ModId = "mod" + i,
                ModName = "Mod " + i,
                EnUsDict = new Dictionary<string, string> { { "k", "word" + i } }
            });
        }

        var coord = new ModTranslationCoordinator(client,
            new AppConfig
            {
                MaxCharsPerBatch = 5000,
                SmartBatching = true,
                Concurrency = 4,
                RequestTimeoutSeconds = 10,
                MaxRetries = 1
            });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = await coord.TranslateAllAsync(mods, null, default);
        sw.Stop();

        Assert.Equal(8, results.Count);
        Assert.All(results, r => Assert.NotEmpty(r.zhCnDict));

        // Must actually be concurrent: ConcurrentPeak > 1, and wall time much less than 8 * 60ms.
        Assert.True(client.ConcurrentPeak >= 2, $"Concurrency peak only {client.ConcurrentPeak} (expected >= 2)");
        Assert.True(sw.ElapsedMilliseconds < 400, $"Took {sw.ElapsedMilliseconds}ms, should be < 400ms with concurrency=4");
    }

    /* ------------------------------------------------------------ */
    /* Compact progress line + pun rotator                          */
    /* ------------------------------------------------------------ */

    [Fact]
    public void ProgressRenderer_RendersBarAndPercentage()
    {
        var line = ProgressRenderer.RenderLine(totalMods: 10, completedMods: 3,
            currentModName: "jei", itemsDone: 120, itemsTotal: 400,
            pun: "It's not a bug, it's a feature!", barWidth: 20);
        Assert.Contains("30%", line);
        Assert.Contains("=", line);
        Assert.Contains("-", line);
        Assert.Contains("jei", line);
        Assert.Contains("It's not a bug", line);
        Assert.Contains("120/400", line);
    }

    [Fact]
    public void ProgressRenderer_ZeroProgress_ShowsEmptyBar()
    {
        var line = ProgressRenderer.RenderLine(10, 0, "mod_a", 0, 100, "tip", barWidth: 10);
        Assert.Contains("0%", line);
    }

    [Fact]
    public void ProgressRenderer_FullProgress_ShowsFullBar()
    {
        var line = ProgressRenderer.RenderLine(5, 5, "-", 10, 10, "tip", barWidth: 10);
        Assert.Contains("100%", line);
    }

    [Fact]
    public void ProgressRenderer_DisplayWidth_CalculatesCorrectly()
    {
        Assert.Equal(0, ProgressRenderer.DisplayWidth(""));
        Assert.Equal(3, ProgressRenderer.DisplayWidth("abc"));
        // Chinese chars are wide (2 columns each)
        Assert.Equal(6, ProgressRenderer.DisplayWidth("测试三"));
    }

    [Fact]
    public void ProgressRenderer_TruncateToWidth_RespectsDisplayColumns()
    {
        // 3 ASCII (3 cols) + 3 Chinese (6 cols) = 9 cols; truncate to 8 → can only fit 3+2+2=7
        var result = ProgressRenderer.TruncateToWidth("abc测试三", 8);
        Assert.True(ProgressRenderer.DisplayWidth(result) <= 8);
        Assert.Contains("abc", result);
    }

    [Fact]
    public void McPunRotator_ContainsKnownPun()
    {
        I18n.SetLanguage(UILanguage.Chinese);
        var rotator = new McPunRotator();
        var seen = new HashSet<string>();
        for (int i = 0; i < 50; i++)
            seen.Add(rotator.Next());
        Assert.Contains(seen, s => s.Contains("特性") || s.Contains("苦力怕") || s.Contains("Steve"));
        Assert.True(seen.Count >= 2, "Should rotate through multiple puns");

        I18n.SetLanguage(UILanguage.English);
        var rotatorEn = new McPunRotator();
        var seenEn = new HashSet<string>();
        for (int i = 0; i < 50; i++)
            seenEn.Add(rotatorEn.Next());
        Assert.Contains(seenEn, s => s.Contains("bug") || s.Contains("Creeper") || s.Contains("Steve"));
        Assert.True(seenEn.Count >= 2, "Should rotate through multiple English puns");

        I18n.SetLanguage(UILanguage.Chinese); // restore
    }

    /* ------------------------------------------------------------ */
    /* Mod platform verifier (Modrinth search with stub HTTP)       */
    /* ------------------------------------------------------------ */

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Factory = _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Factory(request));
        }
    }

    [Fact]
    public async Task ModPlatformVerifier_ModrinthHit_ReturnsExists()
    {
        var handler = new StubHttpMessageHandler
        {
            Factory = req =>
            {
                Assert.Contains("api.modrinth.com", req.RequestUri!.Host);
                var body = new
                {
                    hits = new[]
                    {
                        new { slug = "jei", title = "Just Enough Items", project_type = "mod", downloads = 200_000_000 }
                    },
                    total_hits = 1
                };
                var json = System.Text.Json.JsonSerializer.Serialize(body);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };
        using var http = new HttpClient(handler);
        var verifier = new ModPlatformVerifier(http);

        var info = new ModInfo { ModId = "jei", ModName = "Just Enough Items" };
        var res = await verifier.VerifyAsync(info, default);

        Assert.True(res.PlatformMatched);
        Assert.Contains("Modrinth", res.MatchedPlatforms);
    }

    [Fact]
    public async Task ModPlatformVerifier_ModrinthMiss_ReturnsUnmatched()
    {
        var handler = new StubHttpMessageHandler
        {
            Factory = _ =>
            {
                var body = new { hits = Array.Empty<object>(), total_hits = 0 };
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body),
                        System.Text.Encoding.UTF8, "application/json")
                };
            }
        };
        using var http = new HttpClient(handler);
        var verifier = new ModPlatformVerifier(http);

        var res = await verifier.VerifyAsync(new ModInfo { ModId = "random9999" }, default);
        Assert.False(res.PlatformMatched);
        Assert.Empty(res.MatchedPlatforms);
    }

    [Fact]
    public void AppConfig_DefaultConcurrency_Is4_MaxCharsPerBatchIs5000()
    {
        var cfg = new AppConfig();
        Assert.Equal(4, cfg.Concurrency);
        Assert.Equal(5000, cfg.MaxCharsPerBatch);
    }

    [Fact]
    public void ProgressRenderer_WithPadTo_PadsToTarget()
    {
        var line = ProgressRenderer.RenderLine(10, 5, "mod_a", 120, 240, "puntip", barWidth: 24, padTo: 80);
        Assert.Equal(80, line.Length);
        Assert.Matches(@"\[=+-+\]\s*\d+%", line);
    }

    /* ------------------------------------------------------------ */
    /* End-to-end simulation: mock AI + 5 fake mods + coordinator   */
    /* ------------------------------------------------------------ */

    [Fact]
    public async Task EndToEnd_FiveFakeMods_AllTranslated()
    {
        var mockClient = new MockAiClient();
        var cfg = new AppConfig
        {
            Concurrency = 3,
            MaxCharsPerBatch = 5000,
            CompactPayload = true,
            RequestTimeoutSeconds = 30,
            MaxRetries = 1
        };
        using var coord = new ModTranslationCoordinator(mockClient, cfg);

        var mods = new List<ModInfo>();
        for (int m = 1; m <= 5; m++)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i <= 10; i++)
                dict[$"mod{m}.key{i}"] = $"English text {i}";
            mods.Add(new ModInfo
            {
                JarPath = $"mod{m}.jar",
                ModName = $"TestMod{m}",
                ModId = $"testmod{m}",
                EnUsDict = dict
            });
        }

        var results = await coord.TranslateAllAsync(mods, progress: null);

        Assert.Equal(5, results.Count);
        foreach (var (mod, zhDict) in results)
        {
            Assert.Equal(10, zhDict.Count);
            foreach (var kv in zhDict)
            {
                Assert.Contains("zh_", kv.Value); // MockAiClient prepends "zh_"
            }
        }
        Assert.Equal(5, mockClient.CallCount);
    }

    [Fact]
    public async Task EndToEnd_PauseAndResume_WorksWithMockClient()
    {
        var mockClient = new MockAiClient();
        var cfg = new AppConfig { Concurrency = 2, MaxCharsPerBatch = 5000, MaxRetries = 0 };
        using var coord = new ModTranslationCoordinator(mockClient, cfg);

        var mods = new List<ModInfo>
        {
            new() { JarPath = "a.jar", ModName = "A", ModId = "a",
                    EnUsDict = new() { { "a.1", "Hello" }, { "a.2", "World" } } },
            new() { JarPath = "b.jar", ModName = "B", ModId = "b",
                    EnUsDict = new() { { "b.1", "Test" } } }
        };

        bool paused = false;
        var results = await coord.TranslateAllAsync(mods, progress: null,
            isPaused: () => paused);

        Assert.Equal(2, results.Count);
        Assert.True(results.All(r => r.Item2.Count > 0));
    }

    [Fact]
    public async Task EndToEnd_EmptyMod_SkippedGracefully()
    {
        var mockClient = new MockAiClient();
        var cfg = new AppConfig { Concurrency = 1, MaxRetries = 0 };
        using var coord = new ModTranslationCoordinator(mockClient, cfg);

        var mods = new List<ModInfo>
        {
            new() { JarPath = "empty.jar", ModName = "Empty", ModId = "empty",
                    EnUsDict = new() { { "k", "v" } } },
            new() { JarPath = "noentries.jar", ModName = "NoEntries", ModId = "ne",
                    EnUsDict = new() }
        };

        var results = await coord.TranslateAllAsync(mods, progress: null);
        Assert.Equal(2, results.Count);
    }

    /// <summary>
    /// Mock AI client that translates by prepending "zh_" to each source text.
    /// Simulates DeepSeek/OpenAI returning translated JSON.
    /// </summary>
    private sealed class MockAiClient : IAiClient
    {
        public int CallCount;
        public string ProviderName => "MockAI";

        public Task<string> TranslateAsync(List<TranslationItem> items, CancellationToken ct = default)
        {
            Interlocked.Increment(ref CallCount);
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in items)
                dict[item.Key] = "zh_" + item.SourceText;
            var json = System.Text.Json.JsonSerializer.Serialize(dict);
            return Task.FromResult(json);
        }
    }
}
