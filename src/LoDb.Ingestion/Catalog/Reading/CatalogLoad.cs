using System.Diagnostics.CodeAnalysis;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>A catalog, or why a read has none yet.</summary>
public sealed record CatalogLoad
{
    private CatalogLoad(CatalogLoadStatus status, CatalogSnapshot? catalog, bool refused)
    {
        Status = status;
        Catalog = catalog;
        Refused = refused;
    }

    public CatalogLoadStatus Status { get; }

    /// <summary>The catalog, when <see cref="IsReady"/>.</summary>
    public CatalogSnapshot? Catalog { get; }

    /// <summary>
    /// Whether the queue refused the ingestion (queue full, crawler budget spent): a pending
    /// catalog then comes with no later read but a new one.
    /// </summary>
    public bool Refused { get; }

    [MemberNotNullWhen(true, nameof(Catalog))]
    public bool IsReady => Status == CatalogLoadStatus.Ready;

    internal static CatalogLoad Unknown { get; } = new(CatalogLoadStatus.Unknown, null, false);

    internal static CatalogLoad Ready(CatalogSnapshot catalog) =>
        new(CatalogLoadStatus.Ready, catalog, false);

    internal static CatalogLoad Pending(bool refused) =>
        new(CatalogLoadStatus.Pending, null, refused);
}
