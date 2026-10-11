using LoDb.Domain.Languages;

namespace LoDb.Domain.Tests.Languages;

/// <summary>
/// A Data Dragon language reduces to its base code, with the two Chinese scripts kept apart,
/// and falls back to English.
/// </summary>
public sealed class UiLocaleFromLanguageTests
{
    // The default language of every locale maps back to it (UiLocaleDefaultLanguageTests):
    // these rows are the other variants and a case the defaults do not show.
    [Theory]
    [InlineData("en_AU", UiLocale.En)]
    [InlineData("zh_MY", UiLocale.ZhHans)]
    [InlineData("FR_fr", UiLocale.Fr)]
    public void ALanguageReducesToItsBaseCode(string language, UiLocale expected)
    {
        Assert.Equal(expected, UiLocales.FromLanguage(language));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("x")]
    [InlineData("xx_XX")]
    [InlineData("zh")]
    public void AnUnknownLanguageFallsBackToEnglish(string? language)
    {
        Assert.Equal(UiLocales.Fallback, UiLocales.FromLanguage(language));
        Assert.Equal(UiLocale.En, UiLocales.Fallback);
    }
}
