using LoDb.Domain.Catalog.Modes;

namespace LoDb.Domain.Tests.Catalog.Modes;

/// <summary>
/// The one curated mode list: internal ids never surface, CLASSIC is the Summoner's Rift, and
/// the facet leaves out the queues nobody picks spells for.
/// </summary>
public sealed class GameModeLabelsTests
{
    [Theory]
    [InlineData("CLASSIC", "Summoner's Rift")]
    [InlineData("CHERRY", "Arena")]
    [InlineData("ARSR", "ARSR")]
    [InlineData("KINGPORO", "Legend of the Poro King")]
    public void Up11NamedQueuesCarryRiotsProductName(string mode, string label)
    {
        Assert.Equal(label, GameModeLabels.LabelOf(mode));
    }

    [Theory]
    [InlineData("WIPMODEWIP")]
    [InlineData("RUBY_TRIAL_1")]
    [InlineData("TUTORIAL_MODULE_1")]
    [InlineData("JADE")]
    public void Up11InternalModesHaveNoLabel(string mode)
    {
        Assert.Null(GameModeLabels.LabelOf(mode));
    }

    [Fact]
    public void Up11DisplayKeepsKnownModesInInputOrderOnce()
    {
        var displayed = GameModeLabels.Displayable(
        [
            "CLASSIC", "WIPMODEWIP", "ARAM", "RUBY_TRIAL_1",
            "JADE", "TUTORIAL_MODULE_1", "KINGPORO", "ARAM",
        ]);

        // JADE stays for the interface to label as the LoL Classic edition.
        Assert.Equal(["CLASSIC", "ARAM", "JADE", "KINGPORO"], displayed);
    }

    [Fact]
    public void AnAllInternalListDisplaysNothing()
    {
        Assert.Empty(GameModeLabels.Displayable(["WIPMODEWIP", "KIWI", "RUBY"]));
    }

    [Fact]
    public void TheFacetOffersTheDisplayableQueuesMinusTheNonGames()
    {
        var facetable = GameModeLabels.Facetable;

        Assert.Contains("CLASSIC", facetable);
        Assert.Contains("ARAM", facetable);
        Assert.DoesNotContain("PRACTICETOOL", facetable);
        Assert.DoesNotContain("TUTORIAL", facetable);
        Assert.DoesNotContain("JADE", facetable);
        Assert.All(facetable, mode => Assert.NotNull(GameModeLabels.LabelOf(mode)));
        Assert.All(facetable, mode => Assert.True(GameModeLabels.IsFacetable(mode)));
    }

    [Fact]
    public void FacetValuesKeepTheFacetableModesOfASpell()
    {
        string[] modes = ["CLASSIC", "WIPMODEWIP", "ARAM", "TUTORIAL", "RUBY_TRIAL_1"];

        Assert.Equal(["CLASSIC", "ARAM"], modes.Where(GameModeLabels.IsFacetable));
    }
}
