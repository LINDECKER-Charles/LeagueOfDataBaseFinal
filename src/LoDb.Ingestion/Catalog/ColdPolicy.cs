namespace LoDb.Ingestion.Catalog;

/// <summary>
/// What a read does about data the store does not hold yet (ADR 0003).
/// </summary>
public enum ColdPolicy
{
    /// <summary>
    /// Waits for the ingestion: detail page, picker, build, search, command line.
    /// </summary>
    Synchronous,

    /// <summary>
    /// Answers at once and queues the ingestion for the worker: lists and previews.
    /// </summary>
    Queued,

    /// <summary>Reads what is stored and ingests nothing.</summary>
    StoredOnly,
}
