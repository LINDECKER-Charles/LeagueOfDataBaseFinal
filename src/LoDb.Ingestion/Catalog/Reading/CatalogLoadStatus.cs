namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>Whether a read got its catalog.</summary>
public enum CatalogLoadStatus
{
    /// <summary>The catalog is loaded.</summary>
    Ready,

    /// <summary>
    /// The datasets are not stored yet: queued, refused by the queue, or not asked for by a
    /// stored-only read. A later read may find them.
    /// </summary>
    Pending,

    /// <summary>Data Dragon lists neither the version nor the language.</summary>
    Unknown,
}
