using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using MMCT.Core.Models;

namespace MMCT.Core.Services;

/// <summary>
/// Looks up mods on Modrinth (public, no key) and optionally CurseForge (needs user API key)
/// to confirm a mod is real/popular before translating. CurseForge Core docs:
/// https://docs.curseforge.com/#section/Authentication — requires `curseForgeApiKey` in config.
/// Modrinth search docs: https://docs.modrinth.com/#tag/projects/operation/searchProjects
/// </summary>
public class ModPlatformVerifier
{
    private readonly HttpClient _http;
    private readonly string _curseForgeApiKey;
    private const string UserAgent = "MMCT/1.0 (+https://github.com/mmct)";

    public ModPlatformVerifier(HttpClient http, string? curseForgeApiKey = null)
    {
        _http = http;
        _curseForgeApiKey = curseForgeApiKey ?? "";
        if (string.IsNullOrWhiteSpace(http.DefaultRequestHeaders.UserAgent.ToString()))
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task<ModVerificationResult> VerifyAsync(ModInfo mod, CancellationToken ct = default)
    {
        var result = new ModVerificationResult();
        var queries = BuildCandidateQueries(mod);

        // --- Modrinth (no key) ---
        foreach (var q in queries)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var facet = "[[\"project_type:mod\"]]";
                var url = $"https://api.modrinth.com/v2/search?query={HttpUtility.UrlEncode(q)}&facets={HttpUtility.UrlEncode(facet)}&limit=3";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("User-Agent", UserAgent);
                var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) continue;

                var parsed = await resp.Content.ReadFromJsonAsync<ModrinthSearch>(cancellationToken: ct).ConfigureAwait(false);
                var hit = parsed?.Hits?.FirstOrDefault(h =>
                    (h.Slug != null && (h.Slug.Equals(mod.ModId, StringComparison.OrdinalIgnoreCase) ||
                                        q.Equals(mod.ModId, StringComparison.OrdinalIgnoreCase))) ||
                    (!string.IsNullOrWhiteSpace(mod.ModId) &&
                     (h.Title ?? "").Replace(" ", "").Contains(mod.ModId.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)));
                hit ??= parsed?.Hits?.FirstOrDefault();
                if (hit != null)
                {
                    var platforms = new List<string>(result.MatchedPlatforms) { "Modrinth" };
                    var popularityLabel = hit.Downloads > 0 ?
                        hit.Downloads switch
                        {
                            >= 100_000_000 => "殿堂级 (≥1亿下载)",
                            >= 10_000_000  => "超热门 (≥1000万下载)",
                            >= 1_000_000   => "热门 (≥100万下载)",
                            >= 100_000     => "常见 (≥10万下载)",
                            _              => $"普通 (约{hit.Downloads:N0}下载)"
                        }
                        : null;
                    result = new ModVerificationResult
                    {
                        PlatformMatched = true,
                        MatchedPlatforms = platforms,
                        PopularityLabel = popularityLabel,
                        ProjectSlug = hit.Slug,
                        ErrorNote = result.ErrorNote
                    };
                    break;
                }
            }
            catch (Exception ex)
            {
                result = new ModVerificationResult
                {
                    PlatformMatched = result.PlatformMatched,
                    MatchedPlatforms = result.MatchedPlatforms,
                    PopularityLabel = result.PopularityLabel,
                    ProjectSlug = result.ProjectSlug,
                    ErrorNote = $"Modrinth lookup failed: {ex.Message}"
                };
            }
        }

        // --- CurseForge (optional) ---
        if (!string.IsNullOrWhiteSpace(_curseForgeApiKey) && queries.Count > 0 && !result.PlatformMatched)
        {
            try
            {
                const int McGameId = 432;
                var q0 = HttpUtility.UrlEncode(queries[0]);
                var url = $"https://api.curseforge.com/v1/mods/search?gameId={McGameId}&searchFilter={q0}&pageSize=3&modLoaderType=1&sortField=2";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("x-api-key", _curseForgeApiKey);
                req.Headers.Add("User-Agent", UserAgent);
                var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var parsed = await resp.Content.ReadFromJsonAsync<CurseForgeSearch>(cancellationToken: ct).ConfigureAwait(false);
                    if (parsed?.Data != null && parsed.Data.Count > 0)
                    {
                        var platforms = new List<string>(result.MatchedPlatforms) { "CurseForge" };
                        var first = parsed.Data[0];
                        result = new ModVerificationResult
                        {
                            PlatformMatched = true,
                            MatchedPlatforms = platforms,
                            PopularityLabel = first.DownloadCount > 0
                                ? $"CurseForge: {first.DownloadCount:N0} 下载"
                                : null,
                            ProjectSlug = first.Slug ?? first.Id.ToString(),
                            ErrorNote = result.ErrorNote
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                result = new ModVerificationResult
                {
                    PlatformMatched = result.PlatformMatched,
                    MatchedPlatforms = result.MatchedPlatforms,
                    PopularityLabel = result.PopularityLabel,
                    ProjectSlug = result.ProjectSlug,
                    ErrorNote = (result.ErrorNote != null ? result.ErrorNote + " | " : "") + $"CurseForge lookup failed: {ex.Message}"
                };
            }
        }

        return result;
    }

    private static List<string> BuildCandidateQueries(ModInfo mod)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(mod.ModId))
            set.Add(mod.ModId!);
        if (!string.IsNullOrWhiteSpace(mod.ModName))
        {
            set.Add(mod.ModName!);
            // Strip common prefixes like "服务端加[FTB 团队] ftb-teams..." to help search.
            var cleaned = System.Text.RegularExpressions.Regex.Replace(mod.ModName!, @"^\s*服务端加", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[\[\]【】].*?[\[\]【】]", " ").Trim();
            cleaned = Regex.Replace(cleaned, @"[_-]?(neoforge|forge|fabric|mc\d[\w.-]*)\b", "", RegexOptions.IgnoreCase);
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"v?\d+[\w.+-]*$", "").Trim();
            if (!string.IsNullOrWhiteSpace(cleaned)) set.Add(cleaned);
        }
        if (!string.IsNullOrWhiteSpace(mod.JarPath))
        {
            var fn = Path.GetFileNameWithoutExtension(mod.JarPath!);
            if (!string.IsNullOrWhiteSpace(fn)) set.Add(fn);
        }
        return set.Take(4).ToList();
    }

    private sealed class ModrinthSearch
    {
        [JsonPropertyName("hits")] public List<ModrinthHit>? Hits { get; set; }
        [JsonPropertyName("total_hits")] public int TotalHits { get; set; }
    }

    private sealed class ModrinthHit
    {
        [JsonPropertyName("slug")] public string? Slug { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("downloads")] public long Downloads { get; set; }
        [JsonPropertyName("project_type")] public string? ProjectType { get; set; }
    }

    private sealed class CurseForgeSearch
    {
        [JsonPropertyName("data")] public List<CurseForgeMod>? Data { get; set; }
    }

    private sealed class CurseForgeMod
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("slug")] public string? Slug { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("downloadCount")] public long DownloadCount { get; set; }
    }
}
