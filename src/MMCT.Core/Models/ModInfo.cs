namespace MMCT.Core.Models;

public class ModInfo
{
    public string JarPath { get; set; } = "";
    public string ModName { get; set; } = "";
    public string ModId { get; set; } = "";
    public Dictionary<string, string> LanguageFiles { get; set; } = new();
    public bool HasChinese { get; set; }
    public string? EnUsContent { get; set; }
    public Dictionary<string, string>? EnUsDict { get; set; }
    public Dictionary<string, string>? ZhCnDict { get; set; }
}
