using MMCT.Core.Clients;
using MMCT.Core.Models;

namespace MMCT.Tests;

public class AiClientFactoryTests
{
    [Theory]
    [InlineData("https://api.deepseek.com/v1", "Auto", AiClientFactory.ProviderType.DeepSeek)]
    [InlineData("https://api.openai.com/v1", "Auto", AiClientFactory.ProviderType.OpenAI)]
    [InlineData("https://api.anthropic.com", "Auto", AiClientFactory.ProviderType.Claude)]
    [InlineData("https://generativelanguage.googleapis.com/", "Auto", AiClientFactory.ProviderType.Gemini)]
    [InlineData("https://custom.com/v1", "Auto", AiClientFactory.ProviderType.OpenAI)]
    [InlineData("", "DeepSeek", AiClientFactory.ProviderType.DeepSeek)]
    [InlineData("", "Claude", AiClientFactory.ProviderType.Claude)]
    [InlineData("", "Gemini", AiClientFactory.ProviderType.Gemini)]
    [InlineData("", "OpenAI", AiClientFactory.ProviderType.OpenAI)]
    public void DetectProvider_UrlOrProvider_ReturnsExpected(string baseUrl, string explicitProvider,
        AiClientFactory.ProviderType expected)
    {
        var actual = AiClientFactory.DetectProvider(baseUrl, explicitProvider);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Create_DeepSeek_BuildsClient()
    {
        var cfg = new AppConfig
        {
            ApiBaseUrl = "https://api.deepseek.com/v1",
            ApiKey = "sk-xxx",
            Model = "deepseek-chat",
            Provider = "Auto"
        };
        var client = AiClientFactory.Create(cfg);
        Assert.Equal("DeepSeek", client.ProviderName);
    }

    [Fact]
    public void Create_Claude_BuildsClient()
    {
        var cfg = new AppConfig
        {
            ApiBaseUrl = "",
            ApiKey = "sk-ant-xxx",
            Model = "claude-3-sonnet",
            Provider = "Claude"
        };
        var client = AiClientFactory.Create(cfg);
        Assert.Equal("Claude", client.ProviderName);
    }

    [Fact]
    public void Create_Gemini_BuildsClient()
    {
        var cfg = new AppConfig
        {
            ApiBaseUrl = "",
            ApiKey = "gkey",
            Model = "gemini-1.5-flash",
            Provider = "Gemini"
        };
        var client = AiClientFactory.Create(cfg);
        Assert.Equal("Gemini", client.ProviderName);
    }

    [Fact]
    public void GetAllProviders_ReturnsAllFive()
    {
        var arr = AiClientFactory.GetAllProviders();
        Assert.Equal(5, arr.Length);
        Assert.Contains(AiClientFactory.ProviderType.Auto, arr);
        Assert.Contains(AiClientFactory.ProviderType.OpenAI, arr);
        Assert.Contains(AiClientFactory.ProviderType.DeepSeek, arr);
        Assert.Contains(AiClientFactory.ProviderType.Claude, arr);
        Assert.Contains(AiClientFactory.ProviderType.Gemini, arr);
    }
}
