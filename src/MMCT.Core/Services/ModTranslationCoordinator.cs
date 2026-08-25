using System.Collections.Concurrent;
using System.Text.Json;
using MMCT.Core.Clients;
using MMCT.Core.Models;

namespace MMCT.Core.Services;

/// <summary>Per-mod translation orchestrator with concurrency, retry, progress aggregation,
/// and compact token payloads. Replacement for the sequential "for each mod call engine" pattern.</summary>
public sealed class ModTranslationCoordinator : IDisposable
{
    private readonly IAiClient _client;
    private readonly AppConfig _cfg;
    private readonly CancellationTokenSource _linkedCancel;
    private bool _disposed;

    public ModTranslationCoordinator(IAiClient client, AppConfig cfg, CancellationToken outer = default)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _linkedCancel = outer.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(outer)
            : new CancellationTokenSource();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _linkedCancel.Cancel(); } catch { }
        _linkedCancel.Dispose();
    }

    /// <summary>Overall progress: (completedMods, totalMods, itemsDone, itemsTotal, currentModName).</summary>
    /// <param name="isPaused">Optional pause checker. When it returns true, translation
    /// of not-yet-started mods blocks until it returns false or cancellation fires.</param>
    public async Task<List<(ModInfo mod, Dictionary<string, string> zhCnDict)>> TranslateAllAsync(
        List<ModInfo> mods,
        IProgress<(int completedMods, int totalMods, long itemsDone, long itemsTotal, string currentModName)>? progress,
        CancellationToken ct = default,
        Func<bool>? isPaused = null)
    {
        if (mods == null || mods.Count == 0)
            return new List<(ModInfo, Dictionary<string, string>)>();

        var ctActual = CancellationTokenSource.CreateLinkedTokenSource(ct, _linkedCancel.Token).Token;
        var totalItems = mods.Sum(m => (long)(m.EnUsDict?.Count ?? 0));
        var completedMods = 0;
        var itemsDone = 0L;
        var currentLock = new object();
        var active = new HashSet<string>(StringComparer.Ordinal);

        var results = new ConcurrentBag<(ModInfo, Dictionary<string, string>)>();
        var concurrency = Math.Clamp(_cfg.Concurrency < 1 ? 4 : _cfg.Concurrency, 1, 16);

        using var sem = new SemaphoreSlim(concurrency, concurrency);
        var tasks = new List<Task>(mods.Count);

        foreach (var mod in mods)
        {
            tasks.Add(Task.Run(async () =>
            {
                await sem.WaitAsync(ctActual).ConfigureAwait(false);
                try
                {
                    // Wait while paused (spin until unpaused or cancelled).
                    while (isPaused != null && isPaused() && !ctActual.IsCancellationRequested)
                        await Task.Delay(100, ctActual).ConfigureAwait(false);

                    lock (currentLock)
                    {
                        active.Add(mod.ModName);
                        Report();
                    }

                    var engine = new TranslationEngine(_client,
                        Math.Clamp(_cfg.MaxCharsPerBatch, 200, 100000),
                        _cfg.SmartBatching,
                        _cfg.CompactPayload,
                        _cfg.RequestTimeoutSeconds,
                        Math.Clamp(_cfg.MaxRetries, 0, 10));

                    var zh = await engine.TranslateAllAsync(
                        mod.EnUsDict ?? new Dictionary<string, string>(StringComparer.Ordinal),
                        progress: null,
                        ctActual).ConfigureAwait(false);

                    results.Add((mod, zh));

                    Interlocked.Increment(ref completedMods);
                    Interlocked.Add(ref itemsDone, mod.EnUsDict?.Count ?? 0);

                    lock (currentLock)
                    {
                        active.Remove(mod.ModName);
                        Report();
                    }
                }
                finally
                {
                    sem.Release();
                }
            }, ctActual));
        }

        Exception? firstFail = null;
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (AggregateException aex)
        {
            firstFail = aex.InnerExceptions.FirstOrDefault();
        }
        catch (Exception ex) { firstFail = ex; }
        if (firstFail != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFail).Throw();
        }

        // Preserve input ordering (for stable output listings). Use JarPath as the stable identity
        // (ModInfo instances inside ConcurrentBag are refs to the same objects, but stay safe).
        var orderMap = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < mods.Count; i++)
            orderMap[mods[i].JarPath ?? "__mod_" + i.ToString("D5")] = i;
        return results
            .Select(r => (row: r, idx: orderMap.TryGetValue(r.Item1.JarPath ?? "", out var idx) ? idx : int.MaxValue))
            .OrderBy(x => x.idx)
            .Select(x => x.row)
            .ToList();

        void Report()
        {
            var head = active.Count == 0
                ? (Interlocked.CompareExchange(ref completedMods, 0, 0) < mods.Count ? "启动中" : "-")
                : string.Join("+", active.OrderBy(s => s, StringComparer.Ordinal));
            progress?.Report((
                Interlocked.CompareExchange(ref completedMods, 0, 0),
                mods.Count,
                Interlocked.Read(ref itemsDone),
                totalItems,
                head));
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<ModInfo>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public bool Equals(ModInfo? x, ModInfo? y) => ReferenceEquals(x, y);
        public int GetHashCode(ModInfo obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
