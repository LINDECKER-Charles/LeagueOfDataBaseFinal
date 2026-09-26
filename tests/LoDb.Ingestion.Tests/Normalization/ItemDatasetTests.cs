using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Derived.Items;
using LoDb.Domain.Editions;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Normalization;

/// <summary>
/// Items: debris kept for recipes but flagged, marked-up names reduced (UP 10), Classic twins
/// linked (UP 6).
/// </summary>
public sealed class ItemDatasetTests
{
    [Fact]
    public async Task DebrisIsKeptAndFlagged()
    {
        var items = await ReadAsync("en_US");

        var debris = items.Where(ItemDebris.IsDebris).Select(static item => item.Id);

        Assert.Equal(
            ["2008", "226660", "7050", "772139", "772140"],
            debris.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("en_US", "Fire at Will")]
    [InlineData("fr_FR", "Feu à volonté")]
    public async Task MarkedUpNamesAreReducedToTheName(string language, string name)
    {
        var items = await ReadAsync(language);

        Assert.Equal(name, Single(items, "3901").Name);
    }

    [Fact]
    public async Task ClassicTwinsAreLinkedBothWays()
    {
        var items = await ReadAsync("en_US");

        var modern = Single(items, "1004");
        var classic = Single(items, "771004");

        Assert.Equal(Edition.Classic, classic.Edition);
        Assert.Equal(Twin("771004", Edition.Classic), modern.Counterpart);
        Assert.Equal(Twin("1004", Edition.Modern), classic.Counterpart);
    }

    // 773001 strips to 3001, which is another item today: the names differ, so no twin.
    [Fact]
    public async Task ReusedIdIsNoTwin()
    {
        var items = await ReadAsync("en_US");

        Assert.Null(Single(items, "3001").Counterpart);
        Assert.Null(Single(items, "773001").Counterpart);
        Assert.Equal(Edition.Classic, Single(items, "773001").Edition);
    }

    private static EditionTwin Twin(string id, Edition edition) =>
        new() { Id = id, Name = "Faerie Charm", Edition = edition };

    private static Item Single(IEnumerable<Item> items, string id) =>
        Assert.Single(items, item => item.Id == id);

    private static async Task<IReadOnlyList<Item>> ReadAsync(string language)
    {
        using var harness = ReplayHarness.Create();
        var items = await harness.Datasets.ReadItemsAsync(
            ReplayHarness.Scope(DdragonFixtures.Latest.Value, language), ReplayHarness.Token);
        return items.Entries;
    }
}
