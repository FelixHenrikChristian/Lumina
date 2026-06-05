using Lumina.Core.Models;

namespace Lumina.Core.Tests;

public sealed class DisplayLanguageTests
{
    [Theory]
    [InlineData(null, DisplayLanguage.System)]
    [InlineData("", DisplayLanguage.System)]
    [InlineData("system", DisplayLanguage.System)]
    [InlineData("en", DisplayLanguage.English)]
    [InlineData("en-US", DisplayLanguage.English)]
    [InlineData("zh", DisplayLanguage.SimplifiedChinese)]
    [InlineData("zh-CN", DisplayLanguage.SimplifiedChinese)]
    [InlineData("zh-Hans", DisplayLanguage.SimplifiedChinese)]
    [InlineData("fr-FR", DisplayLanguage.System)]
    public void Normalize_ReturnsSupportedLanguage(string? language, string expected)
    {
        Assert.Equal(expected, DisplayLanguage.Normalize(language));
    }
}
