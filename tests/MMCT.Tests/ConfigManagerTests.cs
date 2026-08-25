using System.Text.Json;
using MMCT.Core.Models;
using MMCT.Core.Services;

namespace MMCT.Tests;

public class ConfigManagerTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mmct_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_NoConfigFile_CreatesDefaultConfigAndSaves()
    {
        var mgr = new ConfigManager(_tempDir);
        var cfg = mgr.Load();
        Assert.NotNull(cfg);
        Assert.True(cfg.SmartBatching);
        Assert.Equal(5000, cfg.MaxCharsPerBatch);
        Assert.Equal(4, cfg.Concurrency);
        Assert.Equal("1.20.1", cfg.GameVersion);
        Assert.True(File.Exists(Path.Combine(_tempDir, "config.json")));
    }

    [Fact]
    public void SaveAndLoad_RoundTripsCustomValues()
    {
        var mgr = new ConfigManager(_tempDir);
        var original = new AppConfig
        {
            ApiBaseUrl = "https://custom.example.com/v1",
            ApiKey = "sk-test-123",
            Model = "gpt-custom",
            Provider = "DeepSeek",
            SmartBatching = false,
            MaxCharsPerBatch = 1234,
            GameVersion = "1.19.2",
            PackDescription = "my pack",
            RequestTimeoutSeconds = 45,
            MaxRetries = 5
        };
        mgr.Save(original);
        var loaded = mgr.Load();

        Assert.Equal(original.ApiBaseUrl, loaded.ApiBaseUrl);
        Assert.Equal(original.ApiKey, loaded.ApiKey);
        Assert.Equal(original.Model, loaded.Model);
        Assert.Equal(original.Provider, loaded.Provider);
        Assert.Equal(original.SmartBatching, loaded.SmartBatching);
        Assert.Equal(original.MaxCharsPerBatch, loaded.MaxCharsPerBatch);
        Assert.Equal(original.GameVersion, loaded.GameVersion);
        Assert.Equal(original.PackDescription, loaded.PackDescription);
        Assert.Equal(original.RequestTimeoutSeconds, loaded.RequestTimeoutSeconds);
        Assert.Equal(original.MaxRetries, loaded.MaxRetries);
    }

    [Fact]
    public void Load_CorruptedJsonFile_ReturnsDefault()
    {
        File.WriteAllText(Path.Combine(_tempDir, "config.json"), "{not valid json!!!");
        var mgr = new ConfigManager(_tempDir);
        var cfg = mgr.Load();
        Assert.NotNull(cfg);
        Assert.Equal(5000, cfg.MaxCharsPerBatch);
    }

    [Fact]
    public void Load_CorruptedJson_DoesNotOverwriteUserFile()
    {
        var path = Path.Combine(_tempDir, "config.json");
        // Trailing comma is a classic manual-edit mistake; it MUST throw a JsonException
        // (System.Text.Json does not allow trailing commas even with comment skip).
        var corrupt = "{ \"apiKey\": \"sk-user-secret\", }";
        File.WriteAllText(path, corrupt);

        var mgr = new ConfigManager(_tempDir);
        var cfg = mgr.Load();

        // Critical: a failed parse must NEVER clobber the user's manual edits.
        Assert.Equal(corrupt, File.ReadAllText(path));
        Assert.NotNull(cfg);
        Assert.False(string.IsNullOrEmpty(mgr.LoadWarning));
    }

    [Fact]
    public void Load_CorruptedJson_SetsLoadWarning()
    {
        File.WriteAllText(Path.Combine(_tempDir, "config.json"), "{broken");
        var mgr = new ConfigManager(_tempDir);
        mgr.Load();
        Assert.False(string.IsNullOrEmpty(mgr.LoadWarning));
    }

    [Fact]
    public void Load_ValidFile_NoWarning()
    {
        var mgr = new ConfigManager(_tempDir);
        mgr.Save(new AppConfig { ApiKey = "sk-ok", Model = "m" });
        var fresh = new ConfigManager(_tempDir);
        fresh.Load();
        Assert.True(string.IsNullOrEmpty(fresh.LoadWarning));
    }

    [Fact]
    public void GetMissingRequiredFields_EmptyConfig_ReturnsApiKey()
    {
        var mgr = new ConfigManager(_tempDir);
        var missing = mgr.GetMissingRequiredFields(new AppConfig());
        Assert.Contains("API Key", missing);
    }

    [Fact]
    public void GetMissingRequiredFields_ApiKeyOnly_IsComplete()
    {
        var mgr = new ConfigManager(_tempDir);
        // Model / BaseUrl are optional: AiClientFactory supplies provider defaults.
        var cfg = new AppConfig { ApiKey = "sk-test" };
        var missing = mgr.GetMissingRequiredFields(cfg);
        Assert.Empty(missing);
    }
}
