using LoDb.Domain.Derived.Items;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Derived.Items;

/// <summary>
/// Unnamed entries, placeholders, markup and unresolved tokens never reach a list or a page.
/// </summary>
public sealed class ItemDebrisTests
{
    [Theory]
    [InlineData("2008", "")]
    [InlineData("7050", "Gangplank Placeholder")]
    public void Up10UnnamedAndPlaceholderItemsAreDebris(string id, string name)
    {
        var item = ItemSamples.Named(id, ItemDebris.DisplayName(name));

        Assert.True(ItemDebris.IsDebris(item));
    }

    [Fact]
    public void Up10MarkedUpNamesAreReducedToTheNameProper()
    {
        const string arenaName = "<rarityLegendary>Feu à volonté</rarityLegendary><br>"
            + "<subtitleLeft><silver>500 serpents</silver></subtitleLeft>";

        var name = ItemDebris.DisplayName(arenaName);

        Assert.Equal("Feu à volonté", name);
        Assert.False(ItemDebris.IsDebris(ItemSamples.Named("3901", name)));
    }

    [Theory]
    [InlineData("{{ Item_Name }}")]
    [InlineData("@ItemName@")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Up10ANameMadeOfUnresolvedTokensIsDebris(string? rawName)
    {
        var item = ItemSamples.Named("9999", ItemDebris.DisplayName(rawName));

        Assert.True(ItemDebris.IsDebris(item));
    }

    [Theory]
    [InlineData("Long Sword", "Long Sword")]
    [InlineData(" Long Sword ", "Long Sword")]
    [InlineData("Doran's Blade", "Doran's Blade")]
    public void APlainNameIsKept(string rawName, string expected)
    {
        var name = ItemDebris.DisplayName(rawName);

        Assert.Equal(expected, name);
        Assert.False(ItemDebris.IsDebris(ItemSamples.Named("1036", name)));
    }
}
