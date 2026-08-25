using System.Text.Json;
using MMCT.Core.Models;

namespace MMCT.Core.Services;

public class ConfigManager
{
    private readonly string _configPath;

    public ConfigManager(string baseDirectory)
    {
        _configPath = Path.Combine(baseDirectory, "config.json");
    }

    /// <summary>Warning message set when the config file could not be loaded (e.g. malformed JSON).</summary>
    public string? LoadWarning { get; private set; }

    public AppConfig Load()
    {
        LoadWarning = null;
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
                if (config != null)
                    return config;
                LoadWarning = $"Config file contains no valid JSON object: {_configPath}";
            }
            else
            {
                var def = new AppConfig();
                Save(def);
                return def;
            }
        }
        catch (Exception ex)
        {
            LoadWarning = $"Failed to parse config.json ({_configPath}): {ex.Message}. " +
                          "Your file was NOT overwritten - please fix the JSON or use menu 3 to reconfigure. " +
                          "配置解析失败，原文件未被修改，请修复 JSON 或使用菜单 3 重新配置。";
        }
        // Never overwrite the user's file on a failed parse: the raw file is preserved
        // so manual edits can be repaired instead of silently lost.
        return new AppConfig();
    }

    /// <summary>Fields that MUST be present before translation can start. Only the API Key is strictly
    /// required - Base URL and Model fall back to per-provider defaults inside AiClientFactory.</summary>
    public List<string> GetMissingRequiredFields(AppConfig config)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            missing.Add("API Key");
        return missing;
    }

    public void Save(AppConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(_configPath, json);
        }
        catch
        {
        }
    }

    public string GetConfigPath() => _configPath;
}
