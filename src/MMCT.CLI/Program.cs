using System.Reflection;
using MMCT.Core;
using MMCT.Core.Clients;
using MMCT.Core.Models;
using MMCT.Core.Services;
using MMCT.Core.UI;

namespace MMCT.CLI;

internal static class Program
{
    private static ConfigManager _configManager = null!;
    private static AppConfig _config = null!;
    private static readonly ModScanner _scanner = new();
    private static readonly ResourcePackBuilder _packBuilder = new();
    private static string _baseDir = null!;
    private static string _exeName = "";

    private static int Main(string[] args)
    {
        _baseDir = AppContext.BaseDirectory;
        try
        {
            // Use the actual on-disk EXE filename (not compiled-in AssemblyName) so that
            // copying / renaming the published binary to MMCT_EN.exe / MMCT_ZH.exe correctly
            // switches the UI language.
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
                _exeName = Path.GetFileNameWithoutExtension(processPath);
            else
                _exeName = Assembly.GetEntryAssembly()?.GetName().Name ?? "MMCT";
        }
        catch { _exeName = "MMCT"; }

        if (_exeName.Contains("_EN", StringComparison.OrdinalIgnoreCase))
            I18n.SetLanguage(UILanguage.English);
        else if (_exeName.Contains("_ZH", StringComparison.OrdinalIgnoreCase))
            I18n.SetLanguage(UILanguage.Chinese);

        if (args.Length > 0 && (args[0].Equals("--en", StringComparison.OrdinalIgnoreCase) ||
                                args[0].Equals("-en", StringComparison.OrdinalIgnoreCase)))
            I18n.SetLanguage(UILanguage.English);
        if (args.Length > 0 && (args[0].Equals("--zh", StringComparison.OrdinalIgnoreCase) ||
                                args[0].Equals("-zh", StringComparison.OrdinalIgnoreCase)))
            I18n.SetLanguage(UILanguage.Chinese);

        _configManager = new ConfigManager(_baseDir);
        _config = _configManager.Load();
        if (!string.IsNullOrEmpty(_configManager.LoadWarning))
            Console.WriteLine(I18n.T("GenericWarn", _configManager.LoadWarning));

        if (args.Any(a => a.Equals("--smoke", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("-s", StringComparison.OrdinalIgnoreCase)))
        {
            PrintHeader();
            Console.WriteLine("[SMOKE] UI language = " + I18n.Current);
            Console.WriteLine("[SMOKE] Config loaded from = " + _configManager.GetConfigPath());
            var detected = AiClientFactory.DetectProvider(_config.ApiBaseUrl, _config.Provider);
            Console.WriteLine("[SMOKE] Detected provider = " + detected);
            Console.WriteLine("[SMOKE] Missing required fields = " +
                              string.Join(", ", _configManager.GetMissingRequiredFields(_config)));
            Console.WriteLine("[SMOKE] Game version = " + _config.GameVersion +
                              " -> pack_format = " + PackFormatMap.GetPackFormat(_config.GameVersion));
            return 0;
        }

        try
        {
            return MainLoop();
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18n.T("GenericError", ex.Message));
            return 1;
        }
    }

    private static int MainLoop()
    {
        while (true)
        {
            Console.Clear();
            PrintHeader();
            Console.WriteLine(I18n.T("MainMenu"));
            Console.WriteLine(I18n.T("Mode1"));
            Console.WriteLine(I18n.T("Mode2"));
            Console.WriteLine(I18n.T("Mode3"));
            Console.WriteLine(I18n.T("Mode4"));
            Console.WriteLine(I18n.T("Mode5"));
            Console.WriteLine(I18n.T("Mode6"));
            Console.WriteLine(I18n.T("Mode0"));
            Console.Write(I18n.T("EnterChoice"));

            var input = Console.ReadLine()?.Trim();
            try
            {
                switch (input)
                {
                    case "1": RunModeOne(); break;
                    case "2": RunModeTwo(); break;
                    case "3": RunAPIMenu(); break;
                    case "4": RunPackMenu(); break;
                    case "5": RunParamMenu(); break;
                    case "6": RunConfigOverview(); break;
                    case "0":
                        Console.WriteLine(I18n.T("ExitMsg"));
                        return 0;
                    default:
                        Console.WriteLine(I18n.T("InvalidChoice"));
                        Pause();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(I18n.T("GenericError", ex.Message));
                Console.WriteLine(ex.StackTrace?.Split('\n').FirstOrDefault());
                Pause();
            }
        }
    }

    private static void PrintHeader()
    {
        Console.WriteLine(I18n.T("AppTitle"));
        Console.WriteLine(I18n.T("Subtitle"));
        var ver = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
        Console.WriteLine($"[{I18n.T("Version")} {ver}]  [{(I18n.Current == UILanguage.Chinese ? "中文界面" : "English UI")}]");
        Console.WriteLine();
    }

    private static void Pause()
    {
        Console.WriteLine(I18n.T("PressAny"));
        Console.ReadKey(true);
    }

    private static string CleanInputPath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        input = input.Trim().Trim('"').Trim('\'');
        if (input.StartsWith("& ")) input = input[2..].Trim();
        return input;
    }

    private static bool Confirm(string message)
    {
        Console.Write(message);
        var k = Console.ReadLine()?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(k) || k == "y" || k == "yes";
    }

    /// <summary>
    /// Re-reads config.json from disk so externally-applied manual edits are picked up
    /// BEFORE any Save() - otherwise a stale in-memory copy would silently clobber them.
    /// </summary>
    private static void ReloadConfig()
    {
        _config = _configManager.Load();
        if (!string.IsNullOrEmpty(_configManager.LoadWarning))
            Console.WriteLine(I18n.T("GenericWarn", _configManager.LoadWarning));
    }

    // --- Mode 1: Full auto ---
    private static void RunModeOne()
    {
        ReloadConfig();
        Console.Clear();
        PrintHeader();
        Console.WriteLine(I18n.T("Mode1Title"));
        Console.Write(I18n.T("PromptVersionFolder"));
        var path = CleanInputPath(Console.ReadLine());
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            Console.WriteLine(I18n.T("FolderNotFound"));
            Pause();
            return;
        }

        var modsDir = _scanner.GetModsDirectoryFromVersionFolder(path);
        if (string.IsNullOrEmpty(modsDir))
        {
            Console.WriteLine(I18n.T("ModsDirNotFound"));
            Pause();
            return;
        }

        var version = _config.GameVersion;
        if (_scanner.TryParseVersionFromFolder(path, out var detected) && !string.IsNullOrEmpty(detected))
        {
            Console.WriteLine(I18n.T("DetectedVersion", detected));
            if (Confirm(I18n.T("ConfirmVersion")))
                version = detected;
        }

        if (version != detected)
        {
            Console.Write(I18n.T("EnterVersionManual"));
            var v = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(v)) version = v;
        }

        _config.GameVersion = version;
        _configManager.Save(_config);

        Console.WriteLine(I18n.T("ScanningMods"));
        var scanProg = new Progress<(int c, int t, string f)>(t =>
        {
            Console.CursorLeft = 0;
            Console.Write(new string(' ', Console.BufferWidth - 1));
            Console.CursorLeft = 0;
            Console.Write(I18n.T("ScanProgress", t.c, t.t, t.f));
        });
        var mods = _scanner.ScanModsFolder(modsDir, scanProg);
        Console.WriteLine();

        var pending = mods.Where(m => !m.HasChinese && m.EnUsDict != null && m.EnUsDict.Count > 0).ToList();
        Console.WriteLine(I18n.T("ScanSummary", mods.Count, pending.Count));

        if (pending.Count == 0)
        {
            Pause();
            return;
        }

        Console.WriteLine(I18n.T("ListPendingMods"));
        for (int i = 0; i < Math.Min(pending.Count, 20); i++)
            Console.WriteLine($"  - {pending[i].ModName} ({pending[i].EnUsDict!.Count} keys)");
        if (pending.Count > 20)
            Console.WriteLine($"  ... + {pending.Count - 20} more");

        // Quick Modrinth (+ optional CurseForge) platform lookup before translation, to filter
        // mod-only jars that are dead libraries / empty placeholders / not published at all.
        VerifyModsAgainstPlatforms(pending);
        if (pending.Count == 0)
        {
            Pause();
            return;
        }

        if (!Confirm(I18n.T("ConfirmStart")))
        {
            Console.WriteLine(I18n.T("Cancelled"));
            Pause();
            return;
        }

        var translations = DoTranslateMods(pending);
        if (translations.Count == 0)
        {
            Console.WriteLine(I18n.T("NoTranslatedMods"));
            Pause();
            return;
        }

        Console.WriteLine(I18n.T("BuildingPack"));
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var zipName = $"MMCT_Translation_Pack_{version}_{timestamp}.zip";
        var zipPath = Path.Combine(_baseDir, zipName);
        var iconPath = _packBuilder.FindIconInFolder(_baseDir);

        _packBuilder.BuildResourcePack(new ResourcePackBuilder.PackBuildOptions
        {
            OutputPath = zipPath,
            GameVersion = version,
            Description = _config.PackDescription,
            IconPath = iconPath,
            Translations = translations
        });

        Console.WriteLine(I18n.T("PackBuilt", zipPath));
        Console.WriteLine(I18n.T("PackFormatInfo", version, PackFormatMap.GetPackFormat(version)));
        Console.WriteLine(I18n.T("SuggestLoadResource"));
        Pause();
    }

    // --- Mode 2: Individual mod ---
    private static void RunModeTwo()
    {
        ReloadConfig();
        Console.Clear();
        PrintHeader();
        Console.WriteLine(I18n.T("Mode2Title"));
        Console.Write(I18n.T("PromptModPath"));
        var path = CleanInputPath(Console.ReadLine());
        if (string.IsNullOrEmpty(path))
        {
            Console.WriteLine(I18n.T("PathNotFound"));
            Pause();
            return;
        }

        List<string> jars;
        if (File.Exists(path) && path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            jars = new List<string> { path };
            Console.WriteLine(I18n.T("FoundSingleJar", Path.GetFileName(path)));
        }
        else if (Directory.Exists(path))
        {
            jars = _scanner.FindModJars(path).ToList();
            Console.WriteLine(I18n.T("FoundMultiJars", jars.Count));
        }
        else
        {
            Console.WriteLine(I18n.T("PathNotFound"));
            Pause();
            return;
        }

        var mods = new List<ModInfo>();
        foreach (var j in jars)
        {
            var m = _scanner.ScanMod(j);
            if (m != null && m.EnUsDict != null && m.EnUsDict.Count > 0)
                mods.Add(m);
        }

        if (mods.Count == 0)
        {
            Console.WriteLine(I18n.T("GenericWarn", "No mod language files found."));
            Pause();
            return;
        }

        VerifyModsAgainstPlatforms(mods);
        if (mods.Count == 0)
        {
            Pause();
            return;
        }

        var translations = DoTranslateMods(mods);
        if (translations.Count == 0)
        {
            Console.WriteLine(I18n.T("NoTranslatedMods"));
            Pause();
            return;
        }

        while (true)
        {
            Console.WriteLine(I18n.T("OutputModeTitle"));
            Console.WriteLine(I18n.T("OutputPack"));
            Console.WriteLine(I18n.T("OutputJson"));
            Console.WriteLine(I18n.T("OutputBack"));
            Console.Write(I18n.T("EnterChoice"));
            var c = Console.ReadLine()?.Trim();
            if (c == "0") break;
            if (c == "1")
            {
                try
                {
                    var outDir = Path.Combine(_baseDir, I18n.T("OutputDirName"));
                    Directory.CreateDirectory(outDir);
                    var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var outZip = Path.Combine(outDir, $"MMCT_Pack_{ts}.zip");
                    var icon = _packBuilder.FindIconInFolder(_baseDir);
                    _packBuilder.BuildResourcePack(new ResourcePackBuilder.PackBuildOptions
                    {
                        OutputPath = outZip,
                        GameVersion = _config.GameVersion,
                        Description = _config.PackDescription,
                        IconPath = icon,
                        Translations = translations
                    });
                    Console.WriteLine(I18n.T("PackBuilt", outZip));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(I18n.T("GenericError", ex.Message));
                }
                Pause();
                break;
            }
            if (c == "2")
            {
                try
                {
                    var outDir = Path.Combine(_baseDir, I18n.T("OutputDirName"));
                    Directory.CreateDirectory(outDir);
                    _packBuilder.ExportZhCnFiles(outDir, translations);
                    Console.WriteLine(I18n.T("ExportedFiles", translations.Count, outDir));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(I18n.T("GenericError", ex.Message));
                }
                Pause();
                break;
            }
            Console.WriteLine(I18n.T("InvalidChoice"));
        }
    }

    private static List<(ModInfo mod, Dictionary<string, string> zhCnDict)> DoTranslateMods(List<ModInfo> mods)
    {
        var results = new List<(ModInfo, Dictionary<string, string>)>();
        var missing = _configManager.GetMissingRequiredFields(_config);
        if (missing.Count > 0)
        {
            Console.WriteLine(I18n.T("ConfigMissingFields", string.Join(", ", missing)));
            Console.WriteLine(I18n.T("ConfigFilePath", _configManager.GetConfigPath()));
            Console.WriteLine(I18n.T("GenericInfo", "请先在菜单 3 中配置 AI API。/ Configure AI API first in menu 3."));
            return results;
        }
        if (string.IsNullOrWhiteSpace(_config.Model))
            Console.WriteLine(I18n.T("GenericInfo", I18n.T("ModelDefaultHint")));

        IAiClient client;
        try
        {
            client = AiClientFactory.Create(_config);
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18n.T("GenericError", ex.Message));
            return results;
        }

        var concurrency = Math.Clamp(_config.Concurrency < 1 ? 4 : _config.Concurrency, 1, 16);
        Console.WriteLine(I18n.T("GenericInfo",
            $"Using provider: {client.ProviderName} | Concurrency: {concurrency} | Batch: {_config.MaxCharsPerBatch:N0} chars" +
            (_config.CompactPayload ? " | Compact (dedupe)" : "")));
        Console.WriteLine(I18n.T("HotkeyHint"));

        using var coord = new ModTranslationCoordinator(client, _config);
        using var cts = new CancellationTokenSource();

        // --- Shared progress state (written by coordinator callbacks, read by display thread) ---
        var pun = new McPunRotator();
        var state = new ProgressState
        {
            TotalMods = mods.Count,
            TotalItems = mods.Sum(m => (long)(m.EnUsDict?.Count ?? 0)),
            Pun = pun.Next()
        };

        // Progress callback: only updates shared state, does NOT write to console.
        var prog = new InlineProgress<(int completedMods, int totalMods, long itemsDone, long itemsTotal, string currentModName)>(p =>
        {
            lock (state)
            {
                state.CompletedMods = p.completedMods;
                state.ItemsDone = p.itemsDone;
                state.CurrentModName = p.currentModName;
                state.IsRunning = true;
            }
        });

        Console.CancelKeyPress += delegate { try { cts.Cancel(); } catch { } };

        // --- Display thread: single-line refresh every 250ms with \r, Pun rotation every 800ms ---
        var displayThread = new Thread(() =>
        {
            int lastPunMs = Environment.TickCount;
            while (!state.StopDisplay)
            {
                Thread.Sleep(250);
                if (state.Paused || state.EscPrompt) continue; // don't render while paused or ESC prompt shown
                var now = Environment.TickCount;
                if (unchecked(now - lastPunMs) > 3500)
                {
                    state.Pun = pun.Next();
                    lastPunMs = now;
                }
                string line;
                lock (state)
                {
                    if (!state.IsRunning && state.CompletedMods == 0)
                        continue;
                    line = ProgressRenderer.RenderLine(
                        state.TotalMods, state.CompletedMods,
                        state.CurrentModName,
                        state.ItemsDone, state.TotalItems,
                        state.Pun, barWidth: 16, padTo: 0);
                }
                try
                {
                    if (Console.IsOutputRedirected) continue;
                    // Truncate to console buffer width - 1 to prevent line wrapping (which causes scrolling)
                    int maxW = Console.BufferWidth > 0 ? Console.BufferWidth - 1 : 79;
                    line = ProgressRenderer.TruncateToWidth(line, maxW);
                    // Pad with spaces to erase leftover chars from previous (longer) line
                    int lineW = ProgressRenderer.DisplayWidth(line);
                    if (lineW < maxW) line += new string(' ', maxW - lineW);
                    Console.Write("\r" + line);
                }
                catch { /* console handle */ }
            }
        }) { IsBackground = true, Name = "MMCT-Display" };
        displayThread.Start();

        // --- Hotkey listener thread: ESC=cancel (with confirmation), Space=toggle pause ---
        var hotkeyThread = new Thread(() =>
        {
            while (!state.StopDisplay)
            {
                try
                {
                    if (Console.IsOutputRedirected) { Thread.Sleep(1000); continue; }
                    if (!Console.KeyAvailable) { Thread.Sleep(80); continue; }
                    var key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.Escape)
                    {
                        // Pause everything first
                        state.EscPrompt = true;
                        state.Paused = true;

                        // Clear the progress line and show confirmation prompt
                        try
                        {
                            int maxW = Console.BufferWidth > 0 ? Console.BufferWidth - 1 : 79;
                            Console.Write("\r" + new string(' ', maxW) + "\r");
                            Console.Write(I18n.T("EscConfirm"));
                        }
                        catch { }

                        // Read user confirmation
                        var resp = Console.ReadLine();
                        state.EscPrompt = false;

                        if (!string.IsNullOrEmpty(resp) &&
                            (resp.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) ||
                             resp.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                             resp.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase)))
                        {
                            state.Pun = ">> Cancelling... <<";
                            try { cts.Cancel(); } catch { }
                        }
                        else
                        {
                            // Resume
                            state.Paused = false;
                        }
                    }
                    else if (key.Key == ConsoleKey.Spacebar)
                    {
                        state.Paused = !state.Paused;
                    }
                }
                catch { Thread.Sleep(200); }
            }
        }) { IsBackground = true, Name = "MMCT-Hotkey" };
        hotkeyThread.Start();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            results = coord.TranslateAllAsync(mods, prog, cts.Token, isPaused: () => state.Paused).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine(I18n.T("GenericWarn", I18n.T("Cancelled")));
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine(I18n.T("GenericError", ex.Message));
        }
        finally
        {
            state.StopDisplay = true;
            sw.Stop();
            // Wait for display thread to fully stop before writing final output
            try { displayThread.Join(1500); } catch { }
            try { hotkeyThread.Join(500); } catch { }
            // Clear the progress line
            try
            {
                if (!Console.IsOutputRedirected)
                {
                    int maxW = Console.BufferWidth > 0 ? Console.BufferWidth - 1 : 79;
                    Console.Write("\r" + new string(' ', maxW) + "\r");
                }
            }
            catch { }
            var total = mods.Sum(m => (long)(m.EnUsDict?.Count ?? 0));
            var done = results.Sum(r => (long)(r.Item2?.Count ?? 0));
            var finalLine = ProgressRenderer.RenderLine(mods.Count, results.Count, I18n.T("ProgressDone"),
                Math.Min(done, total), total, I18n.T("ProgressPunDone"), barWidth: 16, padTo: 0);
            Console.WriteLine(finalLine.TrimEnd());
            Console.WriteLine(I18n.T("TranslateDuration", sw.Elapsed.ToString(@"mm\:ss"), results.Count, mods.Count,
                concurrency));
        }

        return results;
    }

    private sealed class ProgressState
    {
        public int TotalMods;
        public int CompletedMods;
        public long TotalItems;
        public long ItemsDone;
        public string CurrentModName = "";
        public string Pun = "";
        public volatile bool IsRunning;
        public volatile bool Paused;
        public volatile bool EscPrompt;
        public volatile bool StopDisplay;
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public InlineProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }

    /// <summary>Runs platform verification on pending mods and returns a filtered list;
    /// prints per-mod status to the console. Skips by default only mods that both platforms
    /// could not find, unless the user explicitly chooses to skip.</summary>
    private static void VerifyModsAgainstPlatforms(List<ModInfo> pending)
    {
        if (pending.Count == 0) return;
        Console.WriteLine();
        Console.WriteLine(I18n.T("VerifyStart", pending.Count));
        Console.WriteLine(I18n.T("VerifyInfo"));

        var results = new Dictionary<ModInfo, ModVerificationResult>(ReferenceEqualityComparer.Instance);
        using var http = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(6)
        });
        http.Timeout = TimeSpan.FromSeconds(8);
        var verifier = new ModPlatformVerifier(http, _config.CurseForgeApiKey);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(8, 5 * pending.Count)));
        var count = 0;
        for (var i = 0; i < pending.Count; i++)
        {
            var m = pending[i];
            ModVerificationResult r;
            try { r = verifier.VerifyAsync(m, cts.Token).GetAwaiter().GetResult(); }
            catch (Exception ex) { r = new ModVerificationResult { ErrorNote = ex.Message }; }
            results[m] = r;
            count++;
            Console.CursorLeft = 0;
            var line = I18n.T("VerifyProgress", count, pending.Count,
                r.PlatformMatched
                    ? string.Join("+", r.MatchedPlatforms)
                    : I18n.T("VerifyNoMatch"));
            if (r.PopularityLabel != null)
                line += $"  [{r.PopularityLabel}]";
            if (!string.IsNullOrEmpty(r.ErrorNote))
                line += "  " + I18n.T("VerifyErrMini");
            Console.Write(line + new string(' ', 20));
        }
        Console.WriteLine();

        var unmatched = pending.Where(m => !results[m].PlatformMatched).ToList();
        if (unmatched.Count > 0)
        {
            Console.WriteLine(I18n.T("VerifyUnmatched", unmatched.Count));
            foreach (var m in unmatched)
                Console.WriteLine("  · " + m.ModName + (results[m].ErrorNote != null ? "  (" + results[m].ErrorNote + ")" : ""));
            Console.Write(I18n.T("VerifySkipPrompt", unmatched.Count));
            var k = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (k == "y")
            {
                foreach (var m in unmatched) pending.Remove(m);
                Console.WriteLine(I18n.T("VerifySkipped", unmatched.Count));
            }
            else
            {
                Console.WriteLine(I18n.T("VerifyKept"));
            }
        }
        else
        {
            Console.WriteLine(I18n.T("VerifyAllMatched", pending.Count));
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<ModInfo>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public bool Equals(ModInfo? x, ModInfo? y) => ReferenceEquals(x, y);
        public int GetHashCode(ModInfo obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    // --- API menu ---
    private static void RunAPIMenu()
    {
        ReloadConfig();
        while (true)
        {
            Console.Clear();
            PrintHeader();
            Console.WriteLine(I18n.T("APIMenu"));
            Console.WriteLine(I18n.T("CurrentProvider", _config.Provider));
            Console.WriteLine(I18n.T("CurrentBaseUrl", string.IsNullOrEmpty(_config.ApiBaseUrl) ? "<empty>" : _config.ApiBaseUrl));
            var masked = string.IsNullOrEmpty(_config.ApiKey) ? "<empty>" :
                _config.ApiKey.Length <= 8 ? "****" : _config.ApiKey[..4] + new string('*', _config.ApiKey.Length - 8) + _config.ApiKey[^4..];
            Console.WriteLine(I18n.T("CurrentKey", masked));
            Console.WriteLine(I18n.T("CurrentModel", string.IsNullOrEmpty(_config.Model) ? "<empty>" : _config.Model));
            var detected = AiClientFactory.DetectProvider(_config.ApiBaseUrl, _config.Provider);
            Console.WriteLine(I18n.T("DetectedProvider", detected));
            Console.WriteLine();
            Console.WriteLine(I18n.T("APIOption1"));
            Console.WriteLine(I18n.T("APIOption2"));
            Console.WriteLine(I18n.T("APIOption3"));
            Console.WriteLine(I18n.T("APIOption4"));
            Console.WriteLine(I18n.T("APIOption5"));
            Console.WriteLine(I18n.T("APIOption0"));
            Console.Write(I18n.T("EnterChoice"));
            var c = Console.ReadLine()?.Trim();
            switch (c)
            {
                case "1":
                    Console.Write(I18n.T("SelectProvider"));
                    var s = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(s))
                    {
                        var providers = AiClientFactory.GetAllProviders();
                        if (int.TryParse(s, out var idx) && idx >= 0 && idx < providers.Length)
                            _config.Provider = providers[idx].ToString();
                    }
                    break;
                case "2":
                    Console.Write(I18n.T("EnterBaseUrl"));
                    var u = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(u)) _config.ApiBaseUrl = u;
                    break;
                case "3":
                    Console.Write(I18n.T("EnterApiKey"));
                    var k = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(k)) _config.ApiKey = k;
                    break;
                case "4":
                    Console.Write(I18n.T("EnterModel"));
                    var m = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(m)) _config.Model = m;
                    break;
                case "5":
                    TestConnection();
                    Pause();
                    continue;
                case "0":
                    return;
                default:
                    Console.WriteLine(I18n.T("InvalidChoice"));
                    Pause();
                    continue;
            }
            _configManager.Save(_config);
            Console.WriteLine(I18n.T("Saved"));
            Pause();
        }
    }

    private static void TestConnection()
    {
        var missing = _configManager.GetMissingRequiredFields(_config);
        if (missing.Count > 0)
        {
            Console.WriteLine(I18n.T("ConfigMissingFields", string.Join(", ", missing)));
            Console.WriteLine(I18n.T("ConfigFilePath", _configManager.GetConfigPath()));
            return;
        }
        Console.WriteLine(I18n.T("TestingConn"));
        try
        {
            var client = AiClientFactory.Create(_config);
            var sample = new List<TranslationItem>
            {
                new() { Key = "test.hello", SourceText = "Hello World" }
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(10, _config.RequestTimeoutSeconds)));
            var r = client.TranslateAsync(sample, cts.Token).GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(r))
                Console.WriteLine(I18n.T("TestOK", client.ProviderName));
            else
                Console.WriteLine(I18n.T("TestFail", "Empty response"));
        }
        catch (Exception ex)
        {
            Console.WriteLine(I18n.T("TestFail", ex.Message));
        }
    }

    // --- Pack menu ---
    private static void RunPackMenu()
    {
        ReloadConfig();
        while (true)
        {
            Console.Clear();
            PrintHeader();
            Console.WriteLine(I18n.T("PackMenu"));
            Console.WriteLine(I18n.T("CurGameVersion", _config.GameVersion, PackFormatMap.GetPackFormat(_config.GameVersion)));
            Console.WriteLine(I18n.T("CurDescription", _config.PackDescription));
            Console.WriteLine();
            Console.WriteLine(I18n.T("PackOption1"));
            Console.WriteLine(I18n.T("PackOption2"));
            Console.WriteLine(I18n.T("PackOption3"));
            Console.WriteLine(I18n.T("PackOption0"));
            Console.Write(I18n.T("EnterChoice"));
            var c = Console.ReadLine()?.Trim();
            switch (c)
            {
                case "1":
                    Console.WriteLine(I18n.T("AvailableVersions"));
                    var vers = PackFormatMap.GetSupportedVersions();
                    Console.WriteLine(string.Join(", ", vers.Take(20)));
                    Console.Write(I18n.T("EnterGameVer"));
                    var v = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(v)) _config.GameVersion = v;
                    break;
                case "2":
                    Console.Write(I18n.T("EnterDesc"));
                    var d = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(d)) _config.PackDescription = d;
                    break;
                case "3":
                    Console.WriteLine(I18n.T("IconHint"));
                    var icon = _packBuilder.FindIconInFolder(_baseDir);
                    if (icon != null) Console.WriteLine(I18n.T("IconFound", icon));
                    else Console.WriteLine(I18n.T("IconNotFound"));
                    Pause();
                    continue;
                case "0":
                    return;
                default:
                    Console.WriteLine(I18n.T("InvalidChoice"));
                    Pause();
                    continue;
            }
            _configManager.Save(_config);
            Console.WriteLine(I18n.T("Saved"));
            Pause();
        }
    }

    // --- Param menu ---
    private static void RunParamMenu()
    {
        ReloadConfig();
        while (true)
        {
            Console.Clear();
            PrintHeader();
            Console.WriteLine(I18n.T("ParamMenu"));
            Console.WriteLine(I18n.T("CurSmartBatch", _config.SmartBatching ? I18n.T("Enabled") : I18n.T("Disabled")));
            Console.WriteLine(I18n.T("CurMaxChars", _config.MaxCharsPerBatch));
            Console.WriteLine(I18n.T("CurConcurrency", _config.Concurrency));
            Console.WriteLine(I18n.T("CurCompactPayload",
                _config.CompactPayload ? I18n.T("Enabled") : I18n.T("Disabled")));
            Console.WriteLine(I18n.T("CurTimeout", _config.RequestTimeoutSeconds));
            Console.WriteLine(I18n.T("CurRetries", _config.MaxRetries));
            Console.WriteLine(I18n.T("CurCurseForgeKey",
                string.IsNullOrEmpty(_config.CurseForgeApiKey) ? "<empty>" :
                    (_config.CurseForgeApiKey.Length <= 6 ? "****" :
                        _config.CurseForgeApiKey[..3] + "****" + _config.CurseForgeApiKey[^3..])));
            Console.WriteLine();
            Console.WriteLine(I18n.T("ParamOption1"));
            Console.WriteLine(I18n.T("ParamOption2"));
            Console.WriteLine(I18n.T("ParamOption5"));
            Console.WriteLine(I18n.T("ParamOption6"));
            Console.WriteLine(I18n.T("ParamOption3"));
            Console.WriteLine(I18n.T("ParamOption4"));
            Console.WriteLine(I18n.T("ParamOption7"));
            Console.WriteLine(I18n.T("ParamOption0"));
            Console.Write(I18n.T("EnterChoice"));
            var c = Console.ReadLine()?.Trim();
            switch (c)
            {
                case "1":
                    _config.SmartBatching = !_config.SmartBatching;
                    break;
                case "2":
                    Console.Write(I18n.T("EnterMaxChars"));
                    if (int.TryParse(Console.ReadLine(), out var mc) && mc > 0)
                        _config.MaxCharsPerBatch = Math.Min(mc, 100000);
                    else Console.WriteLine(I18n.T("MustBeNumber"));
                    break;
                case "5":
                    Console.Write(I18n.T("EnterConcurrency"));
                    if (int.TryParse(Console.ReadLine(), out var cc) && cc >= 1 && cc <= 16)
                        _config.Concurrency = cc;
                    else Console.WriteLine(I18n.T("MustBeNumberRange", 1, 16));
                    break;
                case "6":
                    _config.CompactPayload = !_config.CompactPayload;
                    break;
                case "3":
                    Console.Write(I18n.T("EnterTimeout"));
                    if (int.TryParse(Console.ReadLine(), out var t) && t > 0)
                        _config.RequestTimeoutSeconds = t;
                    else Console.WriteLine(I18n.T("MustBeNumber"));
                    break;
                case "4":
                    Console.Write(I18n.T("EnterRetries"));
                    if (int.TryParse(Console.ReadLine(), out var r) && r >= 0 && r <= 10)
                        _config.MaxRetries = r;
                    else Console.WriteLine(I18n.T("MustBeNumber"));
                    break;
                case "7":
                    Console.Write(I18n.T("EnterCurseForgeKey"));
                    var cfk = Console.ReadLine()?.Trim();
                    _config.CurseForgeApiKey = cfk ?? "";
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine(I18n.T("InvalidChoice"));
                    Pause();
                    continue;
            }
            _configManager.Save(_config);
            Console.WriteLine(I18n.T("Saved"));
            Pause();
        }
    }

    // --- Config overview ---
    private static void RunConfigOverview()
    {
        ReloadConfig();
        Console.Clear();
        PrintHeader();
        Console.WriteLine(I18n.T("ConfigOverviewTitle"));
        Console.WriteLine(I18n.T("ConfigOverviewFile", _configManager.GetConfigPath()));
        Console.WriteLine();

        Console.WriteLine(I18n.T("ConfigOverviewAI"));
        Console.WriteLine(I18n.T("CfgProvider", _config.Provider));
        Console.WriteLine(I18n.T("CfgApiBaseUrl", string.IsNullOrEmpty(_config.ApiBaseUrl) ? I18n.T("CfgEmpty") : _config.ApiBaseUrl));
        var maskedKey = string.IsNullOrEmpty(_config.ApiKey) ? I18n.T("CfgEmpty") :
            _config.ApiKey.Length <= 8 ? "****" :
            _config.ApiKey[..4] + new string('*', _config.ApiKey.Length - 8) + _config.ApiKey[^4..];
        Console.WriteLine(I18n.T("CfgApiKey", maskedKey));
        Console.WriteLine(I18n.T("CfgModel", string.IsNullOrEmpty(_config.Model) ? I18n.T("CfgEmpty") : _config.Model));
        Console.WriteLine();

        Console.WriteLine(I18n.T("ConfigOverviewPack"));
        Console.WriteLine(I18n.T("CfgGameVersion", _config.GameVersion));
        Console.WriteLine(I18n.T("CfgPackDescription", string.IsNullOrEmpty(_config.PackDescription) ? I18n.T("CfgEmpty") : _config.PackDescription));
        Console.WriteLine();

        Console.WriteLine(I18n.T("ConfigOverviewParam"));
        Console.WriteLine(I18n.T("CfgSmartBatching", _config.SmartBatching ? I18n.T("Enabled") : I18n.T("Disabled")));
        Console.WriteLine(I18n.T("CfgMaxCharsPerBatch", _config.MaxCharsPerBatch));
        Console.WriteLine(I18n.T("CfgConcurrency", _config.Concurrency));
        Console.WriteLine(I18n.T("CfgCompactPayload", _config.CompactPayload ? I18n.T("Enabled") : I18n.T("Disabled")));
        Console.WriteLine(I18n.T("CfgRequestTimeout", _config.RequestTimeoutSeconds));
        Console.WriteLine(I18n.T("CfgMaxRetries", _config.MaxRetries));
        var maskedCf = string.IsNullOrEmpty(_config.CurseForgeApiKey) ? I18n.T("CfgEmpty") :
            _config.CurseForgeApiKey.Length <= 6 ? "****" :
            _config.CurseForgeApiKey[..3] + "****" + _config.CurseForgeApiKey[^3..];
        Console.WriteLine(I18n.T("CfgCurseForgeKey", maskedCf));

        Console.WriteLine();
        Pause();
    }
}
