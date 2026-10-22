using LoDb.Domain.States.Fingerprints;

namespace LoDb.Ingestion.Tests.Catalog.States.Pairs;

/// <summary>
/// Items of the recording between two versions: a text that changes, a redesign, an id Riot
/// gave to another item.
/// </summary>
public sealed class ItemPairTests
{
    [Fact]
    public async Task BootsChangeByTheirTextAlone()
    {
        var before = await CatalogPairs.ItemAsync("7.21.1", "1001");
        var after = await CatalogPairs.ItemAsync("7.22.1", "1001");

        Assert.NotEqual(EntityFingerprint.Of(before), EntityFingerprint.Of(after));
        Assert.Equal(["description"], CatalogPairs.ChangedKeys(before, after));
    }

    [Fact]
    public async Task ARedesignChangesItsStatsAndPrice()
    {
        var before = await CatalogPairs.ItemAsync("8.7.1", "3078");
        var after = await CatalogPairs.ItemAsync("16.18.1", "3078");

        var keys = CatalogPairs.ChangedKeys(before, after);

        Assert.Contains("description", keys);
        Assert.Contains("gold.total", keys);
        Assert.Contains(keys, static key => key.StartsWith("stat", StringComparison.Ordinal));
        Assert.DoesNotContain("name", keys);
    }

    [Fact]
    public async Task AReusedIdChangesNameAndAllFromTheItemBefore()
    {
        var mask = await CatalogPairs.ItemAsync("8.7.1", "3001");
        var evenshroud = await CatalogPairs.ItemAsync("16.18.1", "3001");

        var keys = CatalogPairs.ChangedKeys(mask, evenshroud);

        Assert.Equal("name", keys[0]);
        Assert.Contains("description", keys);
    }
}
