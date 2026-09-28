using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Seo.Files;

/// <summary>What the site publishes right now, as far as it could be read.</summary>
/// <param name="Version">The latest version; null before the first promotion.</param>
/// <param name="Catalog">Its en_US catalog; null when it could not be read.</param>
internal sealed record SiteInventory(PatchVersion? Version, CatalogSnapshot? Catalog)
{
    public static SiteInventory Empty { get; } = new(null, null);
}
