using LoDb.Domain.Text;

namespace LoDb.Domain.Tests.Text;

/// <summary>
/// The template tokens Data Dragon never resolves are stripped; its markup is left to the
/// renderer.
/// </summary>
public sealed class DdragonTextCleanTests
{
    // Spelled as a code point: the character itself is invisible in the source.
    private const char NoBreakSpace = (char)0xA0;

    [Theory]
    [InlineData("Slows by @Slow@ and {{ Item_Cooldown }} reveals.", "Slows by and reveals.")]
    [InlineData(
        "Cooldown: {{ Item_Cooldown }} seconds {{ Item_Melee_Ranged_Split }}",
        "Cooldown: seconds")]
    [InlineData("Heals for @BaseHeal@.", "Heals for .")]
    [InlineData("Deals @f1.Damage@ damage.", "Deals damage.")]
    public void Up10UnresolvedTokensAreStripped(string html, string expected)
    {
        Assert.Equal(expected, DdragonText.Clean(html));
    }

    [Theory]
    [InlineData("<stats>+10</stats>")]
    [InlineData(
        "<mainText><stats><attention>+40</attention> AD</stats><br><passive>Mist</passive>"
        + "</mainText>")]
    [InlineData("Price: 3 000 gold")]
    public void MarkupAndProseAreKept(string html)
    {
        Assert.Equal(html, DdragonText.Clean(html));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NothingCleansToEmpty(string? html)
    {
        Assert.Equal(string.Empty, DdragonText.Clean(html));
    }

    [Fact]
    public void OnlyAsciiPaddingIsTrimmed()
    {
        Assert.Equal("Mist", DdragonText.Clean(" \tMist \n"));
        Assert.Equal("Mist" + NoBreakSpace, DdragonText.Clean(" Mist" + NoBreakSpace + "\n"));
    }
}
