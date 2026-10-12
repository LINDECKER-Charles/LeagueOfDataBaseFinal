using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Derived.Items;
using LoDb.Ingestion.Tests.Ddragon;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Normalization;

/// <summary>
/// Items: debris kept for recipes but flagged, marked-up names reduced (UP 10).
/// </summary>
/// <remarks>
/// The Classic twins (UP 6) are linked by <c>EditionTwins</c>, tested in the Domain; the
/// catalog export shows that the mapper links them (<c>CatalogExportTests</c>).
/// </remarks>
public sealed class ItemDatasetTests
{
    [Fact]
    public async Task DebrisIsKeptAndFlagged()
    {
        var items = await ReadAsync("en_US");

        var debris = items.Where(static item => ItemDebris.IsDebris(item, item.Name))
            .Select(static item => item.Id);

        Assert.Equal(
            ["2008", "226660", "7050", "772139", "772140"],
            debris.Order(StringComparer.Ordinal));
    }

    // The reduction itself is ItemDebris' (Domain): this proves the mapper applies it.
    [Fact]
    public async Task MarkedUpNamesAreReducedToTheName()
    {
        var items = await ReadAsync("fr_FR");

        Assert.Equal("Feu à volonté", Single(items, "3901").Name);
    }

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
