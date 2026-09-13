using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MMCT.Core.Models;

namespace MMCT.Core.Services;

public class ModScanner
{
    public IEnumerable<string> FindModJars(string directory)
    {
        if (!Directory.Exists(directory))
            return Enumerable.Empty<string>();
        return Directory.EnumerateFiles(directory, "*.jar", SearchOption.AllDirectories);
    }

    public ModInfo? ScanMod(string jarPath, bool extractContent = true)
    {
        try
        {
            if (!File.Exists(jarPath)) return null;

            var mod = new ModInfo
            {
                JarPath = jarPath,
                ModName = Path.GetFileNameWithoutExtension(jarPath)
            };

            using var zip = ZipFile.OpenRead(jarPath);
            var langEntries = zip.Entries
                .Where(e => !string.IsNullOrEmpty(e.FullName) &&
                            Regex.IsMatch(e.FullName, @"assets[\\/][^\\/]+[\\/]lang[\\/][a-z]{2}_[a-z]{2}\.(json|lang)$",
                                RegexOptions.IgnoreCase))
                .ToList();

            var modIdSet = new HashSet<string>();
            foreach (var entry in langEntries)
            {
                var match = Regex.Match(entry.FullName, @"assets[\\/]([^\\/]+)[\\/]lang[\\/]([a-z]{2}_[a-z]{2})\.(json|lang)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var modId = match.Groups[1].Value;
                    var langCode = match.Groups[2].Value.ToLowerInvariant();
                    var ext = match.Groups[3].Value.ToLowerInvariant();
                    modIdSet.Add(modId);
                    mod.LanguageFiles[langCode] = entry.FullName;

                    if (langCode == "zh_cn")
                        mod.HasChinese = true;

                    if (extractContent && langCode == "en_us")
                    {
                        mod.LanguageFormat = ext == "lang" ? LanguageFileFormat.Lang : LanguageFileFormat.Json;
                        try
                        {
                            using var sr = new StreamReader(entry.Open());
                            mod.EnUsContent = sr.ReadToEnd();
                            mod.EnUsDict = ParseLanguageFile(mod.EnUsContent, mod.LanguageFormat);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            mod.ModId = modIdSet.Count == 1 ? modIdSet.First() : string.Join("|", modIdSet);

            if (!extractContent)
            {
                zip.Dispose();
            }

            return mod;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static Dictionary<string, string>? ParseLanguageFile(string content, LanguageFileFormat format)
    {
        return format == LanguageFileFormat.Lang ? ParseLangFile(content) : ParseJsonFile(content);
    }

    public static LanguageFileFormat DetectFormatFromPath(string path)
    {
        return path.EndsWith(".lang", StringComparison.OrdinalIgnoreCase)
            ? LanguageFileFormat.Lang
            : LanguageFileFormat.Json;
    }

    private static Dictionary<string, string>? ParseJsonFile(string content)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(val))
                        result[prop.Name] = val;
                }
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string>? ParseLangFile(string content)
    {
        try
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using var sr = new StringReader(content);
            while (sr.ReadLine() is { } line)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#') continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line[..eq].Trim();
                var val = line[(eq + 1)..];
                if (string.IsNullOrWhiteSpace(val)) continue;
                if (key.Length > 0)
                    result[key] = val;
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    public static string SerializeLangFile(Dictionary<string, string> dict)
    {
        var sb = new StringBuilder();
        foreach (var (k, v) in dict)
            sb.Append(k).Append('=').Append(v).Append('\n');
        return sb.ToString();
    }

    public string? GetModsDirectoryFromVersionFolder(string versionFolder)
    {
        if (!Directory.Exists(versionFolder)) return null;

        var di = new DirectoryInfo(versionFolder);
        if (di.Name.Equals("mods", StringComparison.OrdinalIgnoreCase))
            return di.FullName;

        var directMods = Path.Combine(versionFolder, "mods");
        if (Directory.Exists(directMods))
            return directMods;

        if (di.Parent != null)
        {
            var parentMods = Path.Combine(di.Parent.FullName, "mods");
            if (Directory.Exists(parentMods))
                return parentMods;
        }

        var parentParent = di.Parent?.Parent;
        if (parentParent != null)
        {
            var ppMods = Path.Combine(parentParent.FullName, "mods");
            if (Directory.Exists(ppMods))
                return ppMods;
        }

        return null;
    }

    public List<ModInfo> ScanModsFolder(string modsDir, IProgress<(int current, int total, string fileName)>? progress = null)
    {
        var jars = FindModJars(modsDir).ToList();
        var results = new List<ModInfo>();
        var total = jars.Count;
        var processed = 0;

        foreach (var jar in jars)
        {
            processed++;
            var fileName = Path.GetFileName(jar);
            progress?.Report((processed, total, fileName));

            var mod = ScanMod(jar);
            if (mod != null)
                results.Add(mod);
        }
        return results;
    }

    public bool TryParseVersionFromFolder(string versionFolder, out string version)
    {
        version = "";
        var di = new DirectoryInfo(versionFolder);

        var jarFile = di.GetFiles("*.jar").FirstOrDefault();
        if (jarFile != null)
        {
            var match = Regex.Match(jarFile.Name, @"(1\.\d+(\.\d+)?)");
            if (match.Success)
            {
                version = match.Groups[1].Value;
                return true;
            }
        }

        var match2 = Regex.Match(di.Name, @"(1\.\d+(\.\d+)?)");
        if (match2.Success)
        {
            version = match2.Groups[1].Value;
            return true;
        }

        if (di.Parent != null)
        {
            var match3 = Regex.Match(di.Parent.Name, @"(1\.\d+(\.\d+)?)");
            if (match3.Success)
            {
                version = match3.Groups[1].Value;
                return true;
            }
        }
        return false;
    }
}
