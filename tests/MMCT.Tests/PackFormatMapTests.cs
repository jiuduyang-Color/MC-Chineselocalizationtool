using MMCT.Core.Models;
using MMCT.Core.Services;

namespace MMCT.Tests;

public class PackFormatMapTests
{
    [Theory]
    [InlineData("1.20.1", 15)]
    [InlineData("1.20.4", 32)]
    [InlineData("1.21", 34)]
    [InlineData("1.21.4", 42)]
    [InlineData("1.19.2", 9)]
    [InlineData("1.18.2", 8)]
    [InlineData("1.17.1", 7)]
    [InlineData("1.16.5", 6)]
    public void GetPackFormat_KnownVersions_ReturnsCorrectValue(string version, int expected)
    {
        var actual = PackFormatMap.GetPackFormat(version);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetPackFormat_UnknownVersion_FallsBackToNearestPrefix()
    {
        var format = PackFormatMap.GetPackFormat("1.20.99");
        Assert.Equal(32, format);
    }

    [Fact]
    public void GetSupportedVersions_ReturnsNonEmptyList()
    {
        var versions = PackFormatMap.GetSupportedVersions();
        Assert.NotEmpty(versions);
        Assert.Contains("1.20.1", versions);
        Assert.Contains("1.19.2", versions);
    }
}
