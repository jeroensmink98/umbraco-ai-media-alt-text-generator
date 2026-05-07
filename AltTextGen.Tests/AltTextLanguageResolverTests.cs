using AltTextGen.Server.Services;

namespace AltTextGen.Tests;

public class AltTextLanguageResolverTests
{
    [Theory]
    [InlineData(null, "en", "en")]
    [InlineData("", "en", "en")]
    [InlineData("  ", "en", "en")]
    [InlineData("nl-NL", "en", "nl")]
    [InlineData("fr", "en", "fr")]
    [InlineData("PT-br", "en", "pt")]
    [InlineData("invalid-culture", "en", "en")]
    [InlineData("e", "en", "en")]
    public void ResolveOutputLanguageCode_UsesCultureWhenValidElseFallsBack(
        string? requestCulture,
        string configuredOutputLanguage,
        string expectedLanguage)
    {
        string actual = AltTextLanguageResolver.ResolveOutputLanguageCode(requestCulture, configuredOutputLanguage);

        Assert.Equal(expectedLanguage, actual);
    }
}
