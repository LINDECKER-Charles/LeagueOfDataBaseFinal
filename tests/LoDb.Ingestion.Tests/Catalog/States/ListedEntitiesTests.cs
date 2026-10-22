using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Tests.Catalog.States;

/// <summary>
/// The entities that exist in a version for the index, the sitemaps and the pass: those with
/// a page, debris and placeholders left out, each once.
/// </summary>
public sealed class ListedEntitiesTests
{
    [Fact]
    public async Task EntitiesWithAPageAreListedOnce()
    {
        var catalog = await CatalogFixtures.ReadAsync(
            PatchVersion.Parse("16.18.1"),
            CatalogFixtures.English);

        var items = ListedEntities.Items(catalog).Select(static item => item.Id).ToList();
        var paths = ListedEntities.PathsOf(catalog).Select(static path => path.Value).ToList();

        Assert.DoesNotContain("2008", items);
        Assert.Equal(items.Count, items.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(paths.Count, paths.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("champions/Ahri", paths);
        Assert.Contains("runes/8000-precision", paths);
    }
}
