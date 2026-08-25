using System.IO.Compression;
using System.Text.Json;
using MMCT.Core.Models;
using MMCT.Core.Services;

namespace MMCT.Tests;

public class ResourcePackBuilderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ResourcePackBuilder _builder = new();

    public ResourcePackBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mmct_pack_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void BuildResourcePack_CreatesCorrectStructure()
    {
        var zipPath = Path.Combine(_tempDir, "out.zip");
        var mod = new ModInfo
        {
            JarPath = Path.Combine(_tempDir, "fake-mod.jar"),
            ModId = "mymod",
            ModName = "MyMod"
        };
        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "block.minecraft.stone", "石头" },
            { "mymod.hello", "你好" },
            { "item.diamond", "钻石" }
        };

        _builder.BuildResourcePack(new ResourcePackBuilder.PackBuildOptions
        {
            OutputPath = zipPath,
            GameVersion = "1.20.1",
            Description = "Test Pack",
            Translations = new List<(ModInfo, Dictionary<string, string>)> { (mod, dict) }
        });

        Assert.True(File.Exists(zipPath));

        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();

        Assert.Contains("pack.mcmeta", entries);

        var metaEntry = archive.GetEntry("pack.mcmeta");
        Assert.NotNull(metaEntry);
        using var sr = new StreamReader(metaEntry!.Open());
        var meta = JsonDocument.Parse(sr.ReadToEnd());
        Assert.Equal(15, meta.RootElement.GetProperty("pack").GetProperty("pack_format").GetInt32());
        Assert.Equal("Test Pack", meta.RootElement.GetProperty("pack").GetProperty("description").GetString());

        var mymodLang = archive.GetEntry("assets/mymod/lang/zh_cn.json");
        Assert.NotNull(mymodLang);
        var mymodJson = JsonDocument.Parse(new StreamReader(mymodLang!.Open()).ReadToEnd());
        Assert.Equal("你好", mymodJson.RootElement.GetProperty("mymod.hello").GetString());

        var mcLang = archive.GetEntry("assets/minecraft/lang/zh_cn.json");
        Assert.NotNull(mcLang);
        var mcJson = JsonDocument.Parse(new StreamReader(mcLang!.Open()).ReadToEnd());
        Assert.Equal("石头", mcJson.RootElement.GetProperty("block.minecraft.stone").GetString());
        Assert.Equal("钻石", mcJson.RootElement.GetProperty("item.diamond").GetString());
    }

    [Fact]
    public void BuildResourcePack_WithNoTranslations_Throws()
    {
        var zipPath = Path.Combine(_tempDir, "empty.zip");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _builder.BuildResourcePack(new ResourcePackBuilder.PackBuildOptions
            {
                OutputPath = zipPath,
                GameVersion = "1.20.1",
                Translations = new List<(ModInfo, Dictionary<string, string>)>()
            }));
        Assert.Contains("No translations", ex.Message);
        Assert.False(File.Exists(zipPath));
    }

    [Fact]
    public void ExportZhCnFiles_WritesPerModJsonFiles()
    {
        var outDir = Path.Combine(_tempDir, "exports");
        var modA = new ModInfo { JarPath = Path.Combine(_tempDir, "a.jar"), ModId = "modA", ModName = "A" };
        var modB = new ModInfo { JarPath = Path.Combine(_tempDir, "b.jar"), ModId = "modB", ModName = "B" };
        var translations = new List<(ModInfo, Dictionary<string, string>)>
        {
            (modA, new Dictionary<string, string>(StringComparer.Ordinal) { { "modA.x", "X" } }),
            (modB, new Dictionary<string, string>(StringComparer.Ordinal) { { "modB.y", "Y" } }),
        };
        _builder.ExportZhCnFiles(outDir, translations);

        var files = Directory.GetFiles(outDir, "*.json").ToList();
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => Path.GetFileName(f).StartsWith("modA_"));
        Assert.Contains(files, f => Path.GetFileName(f).StartsWith("modB_"));
    }

    [Fact]
    public void FindIconInFolder_NoIcon_ReturnsNull()
    {
        var result = _builder.FindIconInFolder(_tempDir);
        Assert.Null(result);
    }

    [Fact]
    public void FindIconInFolder_WithPackPngAtRoot_ReturnsPath()
    {
        var p = Path.Combine(_tempDir, "pack.png");
        File.WriteAllBytes(p, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var found = _builder.FindIconInFolder(_tempDir);
        Assert.Equal(p, found);
    }

    [Fact]
    public void FindIconInFolder_InResourceIconFolder_ReturnsPath()
    {
        var sub = Path.Combine(_tempDir, "resource_icon");
        Directory.CreateDirectory(sub);
        var p = Path.Combine(sub, "pack.png");
        File.WriteAllBytes(p, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var found = _builder.FindIconInFolder(_tempDir);
        Assert.Equal(p, found);
    }
}
