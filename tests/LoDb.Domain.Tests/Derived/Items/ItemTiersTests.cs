using LoDb.Domain.Derived.Items;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Derived.Items;

/// <summary>
/// Data Dragon has no tier field: it follows from the recipe depth and what an item builds
/// into.
/// </summary>
public sealed class ItemTiersTests
{
    [Fact]
    public void Up13ABaseItemThatBuildsIntoSomethingIsAComponent()
    {
        var boots = ItemSamples.Named("1001", "Boots") with { Into = ["3006"] };

        Assert.Equal(ItemTier.Component, ItemTiers.Of(boots));
    }

    [Theory]
    [InlineData(2, ItemTier.Epic)]
    [InlineData(3, ItemTier.Legendary)]
    [InlineData(4, ItemTier.Legendary)]
    public void Up13DepthTwoIsEpicAndDeeperIsLegendary(int depth, ItemTier expected)
    {
        var item = ItemSamples.Named("3057", "Sheen") with { Depth = depth, Into = ["3078"] };

        Assert.Equal(expected, ItemTiers.Of(item));
    }

    [Fact]
    public void AnItemOutsideAnyRecipeHasNoTier()
    {
        var potion = ItemSamples.Named("2003", "Health Potion") with { IsConsumed = true };
        var locked = ItemSamples.Named("2010", "Locked Biscuit") with
        {
            Gold = ItemSamples.Gold(total: 0, combine: 0) with { IsPurchasable = false },
        };

        Assert.Null(ItemTiers.Of(potion));
        Assert.Null(ItemTiers.Of(locked));
    }
}
