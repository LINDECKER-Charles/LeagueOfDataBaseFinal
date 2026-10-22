using LoDb.Domain.Text;

namespace LoDb.Domain.Tests.Text;

/// <summary>
/// The one plain form of a text the fingerprint and the diff share: no markup, no template
/// token, one line per displayed line, single spaces.
/// </summary>
public sealed class PlainTextTests
{
    [Theory]
    [InlineData(
        "<mainText><stats><attention>+40</attention> AD</stats><br><br><passive>Mist</passive>"
            + "</mainText>",
        "+40 AD\nMist")]
    [InlineData("Heals for @BaseHeal@ over {{ Duration }} seconds.", "Heals for over seconds.")]
    [InlineData("Line one\n   and  the\trest<br/>  Line two", "Line one and the rest\nLine two")]
    [InlineData("<br><li><passive>Spark:</passive> burns.<li>Stacks.", "Spark: burns.\nStacks.")]
    [InlineData("Fish &amp; chips", "Fish & chips")]
    public void MarkupGoesAndLinesStay(string html, string expected)
    {
        Assert.Equal(expected, DdragonText.PlainText(html));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<br><br>")]
    public void NothingShownIsEmpty(string? html)
    {
        Assert.Equal(string.Empty, DdragonText.PlainText(html));
    }
}
