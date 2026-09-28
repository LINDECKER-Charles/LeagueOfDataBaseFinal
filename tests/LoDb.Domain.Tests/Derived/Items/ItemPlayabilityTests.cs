using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Derived.Items;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Derived.Items;

/// <summary>
/// A build carries current-game items flagged on its map (or not flagged at all); the picker
/// further drops what cannot be bought by any champion.
/// </summary>
public sealed class ItemPlayabilityTests
{
    private static readonly Item BerserkerGreaves = ItemSamples.Named("3006", "Berserker Greaves")
        with { Maps = ItemSamples.Maps((11, true), (12, false)) };

    private static readonly Item Boots = ItemSamples.Named("1001", "Boots")
        with { Maps = ItemSamples.Maps((11, true)) };

    private static readonly Item AramOnly = ItemSamples.Named("3070", "Aram Only")
        with { Maps = ItemSamples.Maps((11, false), (12, true)) };

    private static readonly Item ClassicBoots = ItemSamples.Named("771001", "Boots")
        with { Maps = ItemSamples.Maps((11, true), (12, true), (453, true)) };

    private static readonly IReadOnlyList<Item> Dataset =
    [
        BerserkerGreaves,
        Boots,
        AramOnly,
        ItemSamples.Named("2010", "Locked Biscuit") with
        {
            Gold = ItemSamples.Gold(total: 0, combine: 0) with { IsPurchasable = false },
        },
        ItemSamples.Named("7013", "Hidden Ornn Thing") with { IsHiddenFromAll = true },
        ItemSamples.Named("3599", "Kalista Spear") with { RequiredChampion = "Kalista" },
        ClassicBoots,
    ];

    [Fact]
    public void ThePickerOffersBuyableCurrentItemsOfTheMode()
    {
        var pickable = Dataset.Where(
            item => ItemPlayability.IsPickable(item, GameMode.SummonersRift));

        Assert.Equal(["3006", "1001"], pickable.Select(item => item.Id));
    }

    [Fact]
    public void AMapFlagDecidesAvailabilityInTheMode()
    {
        Assert.True(ItemPlayability.IsPickable(AramOnly, GameMode.Aram));
        Assert.False(ItemPlayability.IsAvailableOn(BerserkerGreaves, GameMode.Aram));
    }

    [Fact]
    public void AMissingFlagExcludesNothing()
    {
        Assert.True(ItemPlayability.IsAvailableOn(Boots, GameMode.Aram));
        Assert.True(ItemPlayability.IsAvailableOn(Boots, GameMode.Arena));
    }

    [Fact]
    public void Up6AClassicItemIsPlayableInNoBuildMode()
    {
        Assert.All(
            GameModes.All,
            mode => Assert.False(ItemPlayability.IsAvailableOn(ClassicBoots, mode)));
    }

    [Fact]
    public void UnavailableItemsAreNamedOnce()
    {
        Item[] build = [BerserkerGreaves, BerserkerGreaves, Boots];

        Assert.Equal(["Berserker Greaves"], ItemPlayability.UnavailableNames(build, GameMode.Aram));
        Assert.Empty(ItemPlayability.UnavailableNames(build, GameMode.SummonersRift));
    }

    [Fact]
    public void Up6AClassicItemIsNamedWithItsIdAmongUnavailableItems()
    {
        Item[] build = [Boots, ClassicBoots];

        Assert.Equal(["Boots [771001]"], ItemPlayability.UnavailableNames(build, GameMode.Aram));
    }
}
