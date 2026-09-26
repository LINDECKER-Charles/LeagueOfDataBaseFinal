using LoDb.Domain.Builds.Rules;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Builds.Catalogs;

/// <summary>What the build rules know of a loaded patch.</summary>
internal static class BuildCatalogs
{
    public static BuildCatalog Of(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new BuildCatalog(
            catalog.Champions.Entries.Select(static champion => champion.Summary.Id),
            catalog.Runes.Entries,
            catalog.Items.Entries);
    }
}
