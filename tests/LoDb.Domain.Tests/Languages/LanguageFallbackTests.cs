using LoDb.Domain.Languages;

namespace LoDb.Domain.Tests.Languages;

/// <summary>
/// A missing language falls back to en_US only, and a version without either serves an empty
/// dataset rather than an error.
/// </summary>
public sealed class LanguageFallbackTests
{
    private static readonly DdragonLanguage Arabic = DdragonLanguage.Parse("ar_AE");

    [Fact]
    public void Up2EnUsIsTheOnlyUniversalFallback()
    {
        Assert.Equal([Arabic, DdragonLanguage.EnUs], LanguageFallback.Chain(Arabic));
    }

    [Fact]
    public void EnglishIsTriedOnce()
    {
        Assert.Equal([DdragonLanguage.EnUs], LanguageFallback.Chain(DdragonLanguage.EnUs));
    }

    [Fact]
    public void TheRequestedLanguageWinsWhenPresent()
    {
        var served = LanguageFallback.FirstPresent(Arabic, static _ => true);

        Assert.Equal(Arabic, served);
    }

    [Fact]
    public void Up2AVersionWithoutTheLanguageServesEnUs()
    {
        // ar_AE ships with 49 of the 397 versions.
        var served = LanguageFallback.FirstPresent(
            Arabic,
            language => language == DdragonLanguage.EnUs);

        Assert.Equal(DdragonLanguage.EnUs, served);
    }

    [Fact]
    public void Up1NeitherLanguagePresentMeansAnEmptyDataset()
    {
        // runesReforged.json answers 403 before 7.22.1, in every language.
        var served = LanguageFallback.FirstPresent(Arabic, static _ => false);

        Assert.Null(served);
    }
}
