using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Editions;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Editions;

/// <summary>
/// A twin is the namesake of the other game: the id stem alone is not enough, since Riot
/// reused item ids over the years.
/// </summary>
public sealed class EditionTwinsTests
{
    private static readonly IReadOnlyList<Item> Items = EditionTwins.LinkItems(
    [
        ItemSamples.Named("1036", "Long Sword"),
        ItemSamples.Named("771036", "Long Sword"),
        ItemSamples.Named("221036", "Long Sword"),
        ItemSamples.Named("3001", "Evenshroud"),
        ItemSamples.Named("773001", "Abyssal Scepter"),
    ]);

    private static readonly IReadOnlyList<SummonerSpell> Spells = EditionTwins.LinkSummonerSpells(
    [
        SummonerSpellSamples.Named("SummonerFlash", "Flash", "CLASSIC", "ARAM"),
        SummonerSpellSamples.Named("SummonerFlash_Jade", "Flash", "JADE"),
        SummonerSpellSamples.Named("SummonerHeal", "Heal", "CLASSIC"),
    ]);

    [Fact]
    public void Up6TwinsAreLinkedBothWays()
    {
        Assert.Equal(
            new EditionTwin { Id = "771036", Name = "Long Sword", Edition = Edition.Classic },
            ItemById("1036").Counterpart);
        Assert.Equal(
            new EditionTwin { Id = "1036", Name = "Long Sword", Edition = Edition.Modern },
            ItemById("771036").Counterpart);
    }

    [Fact]
    public void Up6AReusedIdWithAnotherNameIsNoTwin()
    {
        Assert.Null(ItemById("773001").Counterpart);
        Assert.Null(ItemById("3001").Counterpart);
    }

    [Fact]
    public void AnArenaVariantHasNoTwin()
    {
        Assert.Null(ItemById("221036").Counterpart);
    }

    [Fact]
    public void Up6ClassicSpellsAreLinkedToTheirCurrentNamesake()
    {
        Assert.Equal(
            new EditionTwin { Id = "SummonerFlash", Name = "Flash", Edition = Edition.Modern },
            SpellById("SummonerFlash_Jade").Counterpart);
        Assert.Equal("SummonerFlash_Jade", SpellById("SummonerFlash").Counterpart?.Id);
        Assert.Null(SpellById("SummonerHeal").Counterpart);
    }

    [Fact]
    public void AnUnnamedEntryHasNoTwin()
    {
        var items = EditionTwins.LinkItems(
            [ItemSamples.Named("2008", string.Empty), ItemSamples.Named("772008", string.Empty)]);

        Assert.All(items, item => Assert.Null(item.Counterpart));
    }

    private static Item ItemById(string id) => Items.Single(item => item.Id == id);

    private static SummonerSpell SpellById(string id) => Spells.Single(spell => spell.Id == id);
}
