using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MMCT.Core.Models;

namespace MMCT.Core.Services;

public class ResourcePackBuilder
{
    public class PackBuildOptions
    {
        public string OutputPath { get; set; } = "";
        public string GameVersion { get; set; } = "1.20.1";
        public string Description { get; set; } = "MMCT Auto-Translation Pack";
        public string? IconPath { get; set; }
        public List<(ModInfo mod, Dictionary<string, string> zhCnDict)> Translations { get; set; } = new();
    }

    public void BuildResourcePack(PackBuildOptions options)
    {
        var outputDir = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        if (File.Exists(options.OutputPath))
            File.Delete(options.OutputPath);

        if (options.Translations == null || options.Translations.Count == 0)
            throw new InvalidOperationException("No translations to pack.");

        using var archive = ZipFile.Open(options.OutputPath, ZipArchiveMode.Create);

        var packMeta = new
        {
            pack = new
            {
                pack_format = PackFormatMap.GetPackFormat(options.GameVersion),
                description = options.Description
            }
        };
        var metaJson = JsonSerializer.Serialize(packMeta, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        WriteEntry(archive, "pack.mcmeta", metaJson);

        if (!string.IsNullOrEmpty(options.IconPath) && File.Exists(options.IconPath))
        {
            var iconBytes = ProcessIcon(options.IconPath);
            var iconEntry = archive.CreateEntry("pack.png");
            using var iconStream = iconEntry.Open();
            iconStream.Write(iconBytes, 0, iconBytes.Length);
        }

        var grouped = new Dictionary<string, Dictionary<string, string>>();
        var vanillaPrefixes = new HashSet<string>(StringComparer.Ordinal)
        {
            "block","item","entity","fluid","effect","enchantment","advancement","biome",
            "container","death","dim","gui","itemGroup","key","sound","splash","stat",
            "structure","text","tooltip","update","gameMode","generator","menu","options",
            "recipe","selectServer","title","chat","attribute","boss","command","comparator",
            "connect","create","dataPack","difficulty","display","enchant","equivalent","filled",
            "font","gamemode","gamerule","gentle","glue","highlight","indev","inventory","join",
            "keybind","language","landing","legacy","level","link","locale","magic","material",
            "mco","merchant","mount","multiplayer","narrator","note","packet","particle","pattern",
            "permission","player","potion","progress","provider","publish","purpur","quest","rarity",
            "realms","recipe","reducedDebugInfo","resourcePack","scoreboard","selectWorld","server",
            "shared","sign","simulation","spectator","spawner","srp","stat","subtitles","tag",
            "team","texturePack","thread","tip","translation","trident","tutorial","version","vignette",
            "weather","wither","world"
        };
        foreach (var (mod, dict) in options.Translations)
        {
            var defaultModId = GetPrimaryModId(mod);
            foreach (var (key, value) in dict)
            {
                var dotIdx = key.IndexOf('.');
                string modId;
                if (dotIdx > 0)
                {
                    var firstSegment = key[..dotIdx];
                    if (vanillaPrefixes.Contains(firstSegment))
                    {
                        var secondDot = key.IndexOf('.', dotIdx + 1);
                        // 3+ segments (e.g. block.minecraft.stone) -> 2nd segment = namespace
                        if (secondDot > dotIdx + 1)
                            modId = key.Substring(dotIdx + 1, secondDot - dotIdx - 1);
                        // 2 segments (e.g. item.diamond) -> standard MC key, belongs to "minecraft"
                        else
                            modId = "minecraft";
                        if (string.IsNullOrEmpty(modId)) modId = "minecraft";
                    }
                    else
                    {
                        modId = firstSegment;
                    }
                }
                else
                {
                    modId = defaultModId;
                }

                if (!grouped.ContainsKey(modId))
                    grouped[modId] = new Dictionary<string, string>(StringComparer.Ordinal);
                grouped[modId][key] = value;
            }
        }

        foreach (var (modId, langDict) in grouped)
        {
            var path = $"assets/{modId}/lang/zh_cn.json";
            var langJson = JsonSerializer.Serialize(langDict, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            WriteEntry(archive, path, langJson);
        }
    }

    private static string GetPrimaryModId(ModInfo mod)
    {
        if (!string.IsNullOrEmpty(mod.ModId))
        {
            if (!mod.ModId.Contains('|'))
                return mod.ModId;
            return mod.ModId.Split('|')[0];
        }
        return Path.GetFileNameWithoutExtension(mod.JarPath).ToLowerInvariant();
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName.Replace('\\', '/'));
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    public void ExportZhCnFiles(string outputDir, List<(ModInfo mod, Dictionary<string, string> zhCnDict)> translations)
    {
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var grouped = new Dictionary<string, Dictionary<string, string>>();
        foreach (var (mod, dict) in translations)
        {
            var modId = string.IsNullOrEmpty(mod.ModId) || mod.ModId.Contains('|')
                ? Path.GetFileNameWithoutExtension(mod.JarPath)
                : mod.ModId;
            if (!grouped.ContainsKey(modId))
                grouped[modId] = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (k, v) in dict)
                grouped[modId][k] = v;
        }

        foreach (var (modId, langDict) in grouped)
        {
            var safeName = string.Join("_", modId.Split(Path.GetInvalidFileNameChars()));
            var filePath = Path.Combine(outputDir, $"{safeName}_zh_cn.json");
            var json = JsonSerializer.Serialize(langDict, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(filePath, json, new UTF8Encoding(false));
        }
    }

    private static byte[] ProcessIcon(string iconPath)
    {
        var bytes = File.ReadAllBytes(iconPath);
        try
        {
            using var ms = new MemoryStream(bytes);
            using var original = System.Drawing.Image.FromStream(ms);
            var target = new System.Drawing.Bitmap(128, 128);
            using (var g = System.Drawing.Graphics.FromImage(target))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.DrawImage(original, 0, 0, 128, 128);
            }
            using var outMs = new MemoryStream();
            target.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
            return outMs.ToArray();
        }
        catch
        {
            return bytes;
        }
    }

    public string? FindIconInFolder(string folder)
    {
        var candidates = new[]
        {
            Path.Combine(folder, "pack.png"),
            Path.Combine(folder, "icon.png"),
            Path.Combine(folder, "resource_icon", "pack.png")
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path)) return Path.GetFullPath(path);
        }
        var dir = Path.Combine(folder, "resource_icon");
        if (Directory.Exists(dir))
        {
            var found = Directory.EnumerateFiles(dir, "*.png").FirstOrDefault();
            return found != null ? Path.GetFullPath(found) : null;
        }
        return null;
    }
}
