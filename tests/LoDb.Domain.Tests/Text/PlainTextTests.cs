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
    [InlineData("<p>First</p><p>Second</p>", "First\nSecond")]
    [InlineData("Before<ul><li>One</li><li>Two</li></ul>After", "Before\nOne\nTwo\nAfter")]
    [InlineData(
        "<stats>+20 AD</stats>Unique.<rules>Once.</rules><hr>End",
        "+20 AD\nUnique.\nOnce.\nEnd")]
    [InlineData("<passive>Lifesteal</passive> <statGood>up</statGood>", "Lifesteal up")]
    public void MarkupGoesAndLinesStay(string html, string expected)
    {
        Assert.Equal(expected, DdragonText.PlainText(html));
    }

    [Theory]
    [InlineData("Below 30% < 50% health, <b>more</b> hits", "Below 30% < 50% health, more hits")]
    [InlineData("Deals 1<2 times <i>damage", "Deals 1<2 times damage")]
    [InlineData("Cost <<b>50</b>> mana", "Cost <50> mana")]
    [InlineData("Shield<!-- hidden <b>note</b> --> up<!-- unclosed", "Shield up")]
    public void ALessThanThatOpensNoTagIsText(string html, string expected)
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
