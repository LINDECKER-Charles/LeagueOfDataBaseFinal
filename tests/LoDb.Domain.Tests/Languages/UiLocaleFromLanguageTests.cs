using LoDb.Domain.Languages;

namespace LoDb.Domain.Tests.Languages;

/// <summary>
/// A Data Dragon language reduces to its base code, with the two Chinese scripts kept apart,
/// and falls back to English.
/// </summary>
public sealed class UiLocaleFromLanguageTests
{
    [Theory]
    [InlineData("fr_FR", UiLocale.Fr)]
    [InlineData("en_US", UiLocale.En)]
    [InlineData("en_AU", UiLocale.En)]
    [InlineData("es_MX", UiLocale.Es)]
    [InlineData("pt_BR", UiLocale.Pt)]
    [InlineData("zh_CN", UiLocale.ZhHans)]
    [InlineData("zh_MY", UiLocale.ZhHans)]
    [InlineData("zh_TW", UiLocale.ZhHant)]
    [InlineData("FR_fr", UiLocale.Fr)]
    public void ALanguageReducesToItsBaseCode(string language, UiLocale expected)
    {
        Assert.Equal(expected, UiLocales.FromLanguage(language));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("xx_XX")]
    [InlineData("zh")]
    public void AnUnknownLanguageFallsBackToEnglish(string? language)
    {
        Assert.Equal(UiLocales.Fallback, UiLocales.FromLanguage(language));
        Assert.Equal(UiLocale.En, UiLocales.Fallback);
    }
}
