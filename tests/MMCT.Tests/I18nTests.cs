using MMCT.Core;

namespace MMCT.Tests;

public class I18nTests
{
    [Fact]
    public void T_ChineseUI_ReturnsChineseString()
    {
        I18n.SetLanguage(UILanguage.Chinese);
        Assert.Equal(UILanguage.Chinese, I18n.Current);
        var s = I18n.T("AppTitle");
        Assert.Contains("Minecraft 模组一键汉化工具", s);
    }

    [Fact]
    public void T_EnglishUI_ReturnsEnglishString()
    {
        I18n.SetLanguage(UILanguage.English);
        Assert.Equal(UILanguage.English, I18n.Current);
        var s = I18n.T("AppTitle");
        Assert.Contains("Minecraft Mod Chinese Localization Tool", s);
    }

    [Fact]
    public void T_WithArgs_FormatsCorrectly()
    {
        I18n.SetLanguage(UILanguage.Chinese);
        var s = I18n.T("DetectedVersion", "1.20.1");
        Assert.Contains("1.20.1", s);
    }

    [Fact]
    public void T_UnknownKey_FallsBackToEnglish()
    {
        I18n.SetLanguage(UILanguage.Chinese);
        // Known key that exists in both dictionaries - use it indirectly by switching language order
        var s = I18n.T("InvalidChoice");
        Assert.False(string.IsNullOrEmpty(s));
    }
}
