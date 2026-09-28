using LoDb.Domain.Text;

namespace LoDb.Domain.Tests.Text;

/// <summary>
/// A marked-up name reduces to the text before its first line break; a plain name passes.
/// </summary>
public sealed class PlainNameTests
{
    [Theory]
    [InlineData(
        "<rarityLegendary>Feu à volonté</rarityLegendary><br><subtitleLeft><silver>500 serpents"
        + "</silver></subtitleLeft>",
        "Feu à volonté")]
    [InlineData("<b>Shield</b><BR />Price", "Shield")]
    [InlineData("<b>Shield</b><br\t/>Price", "Shield")]
    [InlineData("<b>Unclosed <i", "Unclosed")]
    [InlineData("<b>{{ Name }} Blade</b>", "Blade")]
    public void Up10MarkupIsReducedToTheNameProper(string name, string expected)
    {
        Assert.Equal(expected, DdragonText.PlainName(name));
    }

    [Theory]
    [InlineData("Long Sword", "Long Sword")]
    [InlineData("  Long Sword ", "Long Sword")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void APlainNamePassesThroughTrimmed(string? name, string expected)
    {
        Assert.Equal(expected, DdragonText.PlainName(name));
    }
}
