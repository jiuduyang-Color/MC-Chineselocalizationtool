using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MMCT.Core.Models;
using MMCT.Core.Services;

namespace MMCT.Tests;

public class ModScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ModScanner _scanner = new();

    public ModScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mmct_scan_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    private void CreateDummyModJar(string path, string modId, bool withChinese, Dictionary<string, string> enUsEntries)
    {
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var enUsEntry = zip.CreateEntry($"assets/{modId}/lang/en_us.json");
        using (var s = enUsEntry.Open())
        using (var w = new StreamWriter(s, new UTF8Encoding(false)))
            w.Write(JsonSerializer.Serialize(enUsEntries));

        if (withChinese)
        {
            var zhCnEntry = zip.CreateEntry($"assets/{modId}/lang/zh_cn.json");
            using var s = zhCnEntry.Open();
            using var w = new StreamWriter(s, new UTF8Encoding(false));
            w.Write("{\"" + modId + ".hello\":\"你好\"}");
        }
    }

    [Fact]
    public void ScanMod_WithEnUs_ExtractsDictionaryAndFlagsCorrectly()
    {
        var jar = Path.Combine(_tempDir, "without_zh.jar");
        CreateDummyModJar(jar, "mymod", false, new Dictionary<string, string>
        {
            { "mymod.hello", "Hello" },
            { "mymod.world", "World" }
        });

        var mod = _scanner.ScanMod(jar);
        Assert.NotNull(mod);
        Assert.Equal(Path.GetFullPath(jar), mod!.JarPath);
        Assert.Equal("mymod", mod.ModId);
        Assert.False(mod.HasChinese);
        Assert.NotNull(mod.EnUsDict);
        Assert.Equal(2, mod.EnUsDict!.Count);
        Assert.Equal("Hello", mod.EnUsDict["mymod.hello"]);
        Assert.True(mod.LanguageFiles.ContainsKey("en_us"));
    }

    [Fact]
    public void ScanMod_WithZhCn_MarksHasChineseTrue()
    {
        var jar = Path.Combine(_tempDir, "with_zh.jar");
        CreateDummyModJar(jar, "haszh", true, new Dictionary<string, string>
        {
            { "haszh.a", "Apple" }
        });
        var mod = _scanner.ScanMod(jar);
        Assert.NotNull(mod);
        Assert.True(mod!.HasChinese);
    }

    [Fact]
    public void ScanMod_InvalidJar_ReturnsNull()
    {
        var fake = Path.Combine(_tempDir, "notajar.jar");
        File.WriteAllText(fake, "this is not a zip");
        Assert.Null(_scanner.ScanMod(fake));
    }

    [Fact]
    public void FindModJars_RecursivelyFindsAllJars()
    {
        var dir = Path.Combine(_tempDir, "mods");
        Directory.CreateDirectory(dir);
        var sub = Path.Combine(dir, "sub");
        Directory.CreateDirectory(sub);
        File.Create(Path.Combine(dir, "a.jar")).Dispose();
        File.Create(Path.Combine(dir, "b.jar")).Dispose();
        File.Create(Path.Combine(sub, "c.jar")).Dispose();
        File.WriteAllText(Path.Combine(dir, "readme.txt"), "hi");

        var jars = _scanner.FindModJars(dir).ToList();
        Assert.Equal(3, jars.Count);
    }

    [Fact]
    public void GetModsDirectoryFromVersionFolder_DirectModsFolder_Used()
    {
        var versionFolder = Path.Combine(_tempDir, ".minecraft", "versions", "1.20.1");
        var modsDir = Path.Combine(_tempDir, ".minecraft", "versions", "1.20.1", "mods");
        Directory.CreateDirectory(modsDir);
        var result = _scanner.GetModsDirectoryFromVersionFolder(versionFolder);
        Assert.Equal(Path.GetFullPath(modsDir), result);
    }

    [Fact]
    public void GetModsDirectoryFromVersionFolder_ModsDirAtParentLevel_Found()
    {
        var minecraft = Path.Combine(_tempDir, ".minecraft");
        var mods = Path.Combine(minecraft, "mods");
        var versions = Path.Combine(minecraft, "versions");
        var vDir = Path.Combine(versions, "1.20.1");
        Directory.CreateDirectory(vDir);
        Directory.CreateDirectory(mods);

        var result = _scanner.GetModsDirectoryFromVersionFolder(vDir);
        Assert.Equal(Path.GetFullPath(mods), result);
    }

    [Fact]
    public void TryParseVersionFromFolder_VersionInFolderName_Succeeds()
    {
        var dir = Path.Combine(_tempDir, "1.20.1");
        Directory.CreateDirectory(dir);
        Assert.True(_scanner.TryParseVersionFromFolder(dir, out var v));
        Assert.Equal("1.20.1", v);
    }

    [Fact]
    public void ScanModsFolder_WithProgress_ReportsAllFiles()
    {
        var modsDir = Path.Combine(_tempDir, "mods");
        Directory.CreateDirectory(modsDir);
        CreateDummyModJar(Path.Combine(modsDir, "a.jar"), "moda", false,
            new Dictionary<string, string> { { "moda.a", "A" } });
        CreateDummyModJar(Path.Combine(modsDir, "b.jar"), "modb", true,
            new Dictionary<string, string> { { "modb.b", "B" } });

        var reports = new List<(int c, int t, string f)>();
        var progress = new Progress<(int, int, string)>(t => reports.Add((t.Item1, t.Item2, t.Item3)));
        var mods = _scanner.ScanModsFolder(modsDir, progress);

        Assert.Equal(2, mods.Count);
        Assert.Equal(2, reports.Count);
        Assert.All(reports, r => Assert.Equal(2, r.t));
    }
}
