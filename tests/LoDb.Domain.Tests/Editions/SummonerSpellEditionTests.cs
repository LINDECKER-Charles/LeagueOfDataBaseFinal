using LoDb.Domain.Editions;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Editions;

/// <summary>
/// A classic summoner spell is played in the JADE mode and carries the "_Jade" suffix.
/// </summary>
public sealed class SummonerSpellEditionTests
{
    [Fact]
    public void Up6JadeModeMakesAClassicSpell()
    {
        var spell = SummonerSpellSamples.Named("SummonerFlash_Jade", "Flash", "JADE");

        Assert.Equal(Edition.Classic, spell.Edition);
    }

    [Fact]
    public void Up6JadeSuffixAloneMakesAClassicSpell()
    {
        // The legacy rule read the modes only and called this spell modern: callers that hold
        // nothing but the id (rankings, links) must reach the same edition as the pages.
        var spell = SummonerSpellSamples.Named("SummonerFlash_Jade", "Flash");

        Assert.Equal(Edition.Classic, spell.Edition);
    }

    [Fact]
    public void ACurrentGameSpellIsModern()
    {
        var spell = SummonerSpellSamples.Named("SummonerFlash", "Flash", "CLASSIC", "ARAM");

        Assert.Equal(Edition.Modern, spell.Edition);
    }

    [Theory]
    [InlineData("SummonerFlash", "SummonerFlash_Jade")]
    [InlineData("SummonerFlash_Jade", "SummonerFlash")]
    public void TheTwinIdTogglesTheSuffix(string id, string expected)
    {
        Assert.Equal(expected, SummonerSpellEdition.CounterpartId(id));
    }
}
