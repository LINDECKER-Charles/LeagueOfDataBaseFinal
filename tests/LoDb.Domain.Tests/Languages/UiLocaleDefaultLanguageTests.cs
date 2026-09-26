using LoDb.Domain.Languages;

namespace LoDb.Domain.Tests.Languages;

/// <summary>
/// Each locale opens on one Data Dragon language, and that language maps back to it.
/// </summary>
public sealed class UiLocaleDefaultLanguageTests
{
    [Theory]
    [InlineData(UiLocale.Fr, "fr_FR")]
    [InlineData(UiLocale.Pt, "pt_BR")]
    [InlineData(UiLocale.Es, "es_ES")]
    [InlineData(UiLocale.En, "en_US")]
    [InlineData(UiLocale.Ar, "ar_AE")]
    [InlineData(UiLocale.ZhHans, "zh_CN")]
    [InlineData(UiLocale.ZhHant, "zh_TW")]
    public void ALocaleOpensOnItsDefaultLanguage(UiLocale locale, string language)
    {
        Assert.Equal(language, UiLocales.DefaultLanguage(locale).Code);
    }

    [Fact]
    public void EveryDefaultLanguageMapsBackToItsLocale()
    {
        var roundTrips = UiLocales.All
            .Select(locale => UiLocales.FromLanguage(UiLocales.DefaultLanguage(locale).Code));

        Assert.Equal(UiLocales.All, roundTrips);
    }
}
