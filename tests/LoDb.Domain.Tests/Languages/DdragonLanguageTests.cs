using LoDb.Domain.Languages;

namespace LoDb.Domain.Tests.Languages;

/// <summary>
/// A Data Dragon language is data: any code of the right shape is accepted, and the shape
/// keeps it safe as a path segment.
/// </summary>
public sealed class DdragonLanguageTests
{
    [Theory]
    [InlineData("fr_FR")]
    [InlineData("en_US")]
    [InlineData("es_MX")]
    [InlineData("xx_XX")]
    public void AnyCodeOfTheRightShapeIsALanguage(string code)
    {
        Assert.True(DdragonLanguage.TryParse(code, out var language));
        Assert.Equal(code, language.Code);
        Assert.Equal(code, language.ToString());
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("fr-FR")]
    [InlineData("FR_fr")]
    [InlineData("fr_FR\n")]
    [InlineData("../_FR")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsRejected(string? code)
    {
        Assert.False(DdragonLanguage.TryParse(code, out var language));
        Assert.Null(language);
    }

    [Fact]
    public void ParseRejectsAMalformedCodeLoudly()
    {
        Assert.Throws<FormatException>(() => DdragonLanguage.Parse("fr-FR"));
    }

    [Fact]
    public void EnglishIsTheSameValueWhereverItComesFrom()
    {
        Assert.Equal(DdragonLanguage.EnUs, DdragonLanguage.Parse("en_US"));
    }
}
