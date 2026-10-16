using LoDb.Domain.Derived.Summoners;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Derived.Summoners;

/// <summary>Placeholder spells never reach a list or a page, in any language.</summary>
public sealed class SummonerDebrisTests
{
    [Theory]
    [InlineData("Summoner_UltBookPlaceholder", "Placeholder")]
    [InlineData("Summoner_UltBookSmitePlaceholder", "Placeholder and Attack-Smite")]
    public void APlaceholderSpellIsDebris(string id, string name)
    {
        var spell = SummonerSpellSamples.Named(id, name, "ULTBOOK");

        Assert.True(SummonerDebris.IsDebris(spell, name));
    }

    [Fact]
    public void APlaceholderMarkedInItsIdAloneIsDebris()
    {
        var spell = SummonerSpellSamples.Named("Summoner_UltBookPlaceholder", "Ultimate slot");

        Assert.True(SummonerDebris.IsDebris(spell, "Ultimate slot"));
    }

    [Fact]
    public void ATranslatedPlaceholderIsDebrisByItsEnglishName()
    {
        var spell = SummonerSpellSamples.Named("SummonerFuture", "占位符");

        Assert.True(SummonerDebris.IsDebris(spell, "Placeholder"));
    }

    [Fact]
    public void WithoutAnEnglishNameThePlaceholderIsReadInTheSpellName()
    {
        Assert.True(SummonerDebris.IsDebris(SummonerSpellSamples.Named("A", "Placeholder"), null));
        Assert.False(SummonerDebris.IsDebris(SummonerSpellSamples.Named("A", "占位符"), null));
    }

    [Fact]
    public void AnOrdinarySpellIsNotDebrisInAnyLanguage()
    {
        var spell = SummonerSpellSamples.Named("SummonerFlash", "Saut éclair", "CLASSIC");

        Assert.False(SummonerDebris.IsDebris(spell, "Flash"));
    }
}
