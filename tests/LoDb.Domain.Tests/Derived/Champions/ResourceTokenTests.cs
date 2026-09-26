using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Champions;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Derived.Champions;

/// <summary>
/// The resource token comes from the en_US entry of the same champion, so a filter means the
/// same thing in every locale.
/// </summary>
public sealed class ResourceTokenTests
{
    [Theory]
    [InlineData("Energy", "Énergie", "energy")]
    [InlineData("None", "Aucune", "none")]
    [InlineData("Blood Well", "Puits de sang", "blood-well")]
    [InlineData("Mana", "Mana", "mana")]
    [InlineData("", "", "none")]
    public void Up13TheTokenComesFromTheEnglishEntry(string english, string french, string expected)
    {
        var token = ResourceToken.Of(Champion("Akali", english), Champion("Akali", french));

        Assert.Equal(expected, token);
    }

    [Fact]
    public void WithoutAnEnglishEntryTheLocalizedOneIsTokenized()
    {
        Assert.Equal("nergie", ResourceToken.Of(null, Champion("Akali", "Énergie")));
    }

    [Fact]
    public void Up3AVersionWithoutPartypeReadsAsNone()
    {
        var champion = ChampionSamples.Named("Ahri", "Ahri");

        Assert.Equal(ResourceToken.None, ResourceToken.Of(champion, champion));
    }

    [Theory]
    [InlineData("  Crimson Rush ", "crimson-rush")]
    [InlineData("Flow/Grit", "flow-grit")]
    [InlineData("---", "none")]
    public void RunsOfOtherCharactersBecomeOneHyphen(string partype, string expected)
    {
        Assert.Equal(expected, ResourceToken.Of(Champion("Yone", partype), Champion("Yone", "")));
    }

    private static ChampionSummary Champion(string id, string partype) =>
        ChampionSamples.Named(id, id) with { Partype = partype };
}
