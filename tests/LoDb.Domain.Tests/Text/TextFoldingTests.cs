using LoDb.Domain.Text;

namespace LoDb.Domain.Tests.Text;

/// <summary>
/// Accents fold to their base letter; everything else stays.
/// </summary>
public sealed class TextFoldingTests
{
    [Theory]
    [InlineData("Épée", "Epee")]
    [InlineData("Feu à volonté", "Feu a volonte")]
    [InlineData("Kog'Maw", "Kog'Maw")]
    [InlineData("Øhmwrecker", "Øhmwrecker")]
    [InlineData("阿狸", "阿狸")]
    [InlineData("", "")]
    public void DiacriticsAreRemoved(string text, string expected)
    {
        Assert.Equal(expected, TextFolding.RemoveDiacritics(text));
    }
}
