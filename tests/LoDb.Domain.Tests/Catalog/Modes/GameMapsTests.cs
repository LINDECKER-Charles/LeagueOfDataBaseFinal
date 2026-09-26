using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Tests.Catalog.Samples;

namespace LoDb.Domain.Tests.Catalog.Modes;

/// <summary>
/// An item is available on the maps flagged true, never on 453 (an edition) nor 22 (TFT).
/// </summary>
public sealed class GameMapsTests
{
    [Fact]
    public void AvailabilityReadsTheTrueFlagsOnly()
    {
        var maps = ItemSamples.Maps(
            (11, true), (12, false), (21, true), (22, true), (35, true), (453, true));

        Assert.Equal(
            [GameMap.SummonersRift, GameMap.NexusBlitz, GameMap.Brawl],
            GameMaps.AvailableOn(maps));
    }

    [Fact]
    public void AnItemWithoutFlagsIsAvailableNowhere()
    {
        Assert.Empty(GameMaps.AvailableOn(null));
        Assert.Empty(GameMaps.AvailableOn(ItemSamples.Maps()));
    }

    [Fact]
    public void TheMapsAreValuedByTheirDdragonIds()
    {
        Assert.Equal([11, 12, 21, 30, 33, 35], GameMaps.All.Select(map => (int)map));
    }
}
