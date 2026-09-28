using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Editions;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Editions;

/// <summary>
/// The classic item catalogue is the "77" six-digit range, whatever the map flags say.
/// </summary>
public sealed class ItemEditionTests
{
    [Theory]
    [InlineData("1004", Edition.Modern)]
    [InlineData("771004", Edition.Classic)]
    [InlineData("773070", Edition.Classic)]
    [InlineData("221011", Edition.Modern)]
    [InlineData("664011", Edition.Modern)]
    [InlineData("7710", Edition.Modern)]
    [InlineData("7710040", Edition.Modern)]
    [InlineData("77１００4", Edition.Modern)]
    public void Up6ClassicItemsAreThe77Range(string id, Edition expected)
    {
        Assert.Equal(expected, ItemEdition.Of(id));
    }

    [Fact]
    public void Up6EditionIgnoresTheMapFlags()
    {
        var flaggedOnTheClassicRift = ItemSamples.Named("1004", "Faerie Charm") with
        {
            Maps = ItemSamples.Maps((11, true), (ItemEdition.ClassicRiftMapId, true)),
        };
        var flaggedOnAram = ItemSamples.Named("771004", "Faerie Charm") with
        {
            Maps = ItemSamples.Maps(((int)GameMap.HowlingAbyss, true)),
        };

        Assert.Equal(Edition.Modern, flaggedOnTheClassicRift.Edition);
        Assert.Equal(Edition.Classic, flaggedOnAram.Edition);
    }

    [Theory]
    [InlineData("771004", "1004")]
    [InlineData("1004", "771004")]
    [InlineData("221011", null)]
    [InlineData("7710040", null)]
    public void TheTwinIdTogglesThePrefix(string id, string? expected)
    {
        Assert.Equal(expected, ItemEdition.CounterpartId(id));
    }

    [Fact]
    public void AClassicItemClaimsTheClassicRiftOnly()
    {
        var withClassicRift = ItemSamples.Maps((12, true), (453, true));
        var withoutClassicRift = ItemSamples.Maps((11, true), (12, true));

        Assert.Equal([453], ItemEdition.ClaimableMapIds("771004", withClassicRift));
        Assert.Equal([453], ItemEdition.ClaimableMapIds("771500", withoutClassicRift));
    }

    [Fact]
    public void ACurrentItemClaimsItsTrueFlagsButTheClassicRift()
    {
        var maps = ItemSamples.Maps((453, true), (12, true), (21, false), (11, true));

        Assert.Equal([11, 12], ItemEdition.ClaimableMapIds("1004", maps));
        Assert.Empty(ItemEdition.ClaimableMapIds("1004", ItemSamples.Maps()));
    }

    [Theory]
    [InlineData("1004", "Faerie Charm")]
    [InlineData("771004", "Faerie Charm [771004]")]
    public void AClassicItemIsQualifiedByItsIdInFlatLists(string id, string expected)
    {
        Assert.Equal(expected, ItemEdition.QualifiedName(id, "Faerie Charm"));
    }
}
