using System.Text.Json;
using LoDb.Domain.Languages;

namespace LoDb.Domain.Tests.Languages;

/// <summary>
/// The 21 locale codes of the URLs, as ADR 0005 lists them, and the serializer writes the
/// same codes.
/// </summary>
public sealed class UiLocaleCodeTests
{
    private static readonly string[] AdrCodes =
    [
        "ar", "cs", "de", "el", "en", "es", "fr", "hu", "id", "it", "ja", "ko", "pl", "pt", "ro",
        "ru", "th", "tr", "vi", "zh-hans", "zh-hant",
    ];

    [Fact]
    public void TheCodesAreThoseOfTheAdrInOrder()
    {
        Assert.Equal(AdrCodes, UiLocales.All.Select(UiLocales.Code));
    }

    [Fact]
    public void TheKebabCaseNamingPolicyWritesTheSameCodes()
    {
        var serialized = UiLocales.All
            .Select(locale => JsonNamingPolicy.KebabCaseLower.ConvertName(locale.ToString()));

        Assert.Equal(AdrCodes, serialized);
    }

    [Theory]
    [InlineData("fr", UiLocale.Fr)]
    [InlineData("zh-hans", UiLocale.ZhHans)]
    [InlineData("zh-hant", UiLocale.ZhHant)]
    public void ACodeReadsBackAsItsLocale(string code, UiLocale expected)
    {
        Assert.True(UiLocales.TryParse(code, out var locale));
        Assert.Equal(expected, locale);
    }

    [Theory]
    [InlineData("FR")]
    [InlineData("fr_FR")]
    [InlineData("zh")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsNoLocale(string? code)
    {
        Assert.False(UiLocales.TryParse(code, out _));
    }
}
