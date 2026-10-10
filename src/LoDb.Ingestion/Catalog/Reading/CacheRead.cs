using System.Diagnostics.CodeAnalysis;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>
/// What the catalog cache answers for a key: the catalog, a dataset missing from the store,
/// or a load the cold gate refused.
/// </summary>
internal sealed record CacheRead
{
    private CacheRead(CatalogSnapshot? catalog, bool isRefused)
    {
        Catalog = catalog;
        IsRefused = isRefused;
    }

    /// <summary>A dataset left the store, or never was in it.</summary>
    public static CacheRead Missing { get; } = new(null, false);

    /// <summary>The gate did not admit the load: a later read may be.</summary>
    public static CacheRead Refused { get; } = new(null, true);

    public CatalogSnapshot? Catalog { get; }

    public bool IsRefused { get; }

    [MemberNotNullWhen(true, nameof(Catalog))]
    public bool IsHeld => Catalog is not null;

    public static CacheRead Of(CatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new CacheRead(catalog, false);
    }
}
