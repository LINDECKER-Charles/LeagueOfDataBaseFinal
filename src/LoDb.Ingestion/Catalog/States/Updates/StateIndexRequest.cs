namespace LoDb.Ingestion.Catalog.States.Updates;

/// <summary>An update of the index of states: who asks, and what it may do besides.</summary>
public sealed record StateIndexRequest
{
    public required StateIndexTrigger Trigger { get; init; }

    /// <summary>
    /// Rebuilds the index into a new generation even when the current one could be extended:
    /// after a change of the stored en_US entries (a mapper change). Builds only.
    /// </summary>
    public bool ShouldRebuild { get; init; }

    /// <summary>
    /// Ingests first the en_US datasets of every listed version up to the latest one that the
    /// store lacks, so that the coverage spans every listed version (en_US only).
    /// </summary>
    public bool ShouldIngestMissing { get; init; }
}
