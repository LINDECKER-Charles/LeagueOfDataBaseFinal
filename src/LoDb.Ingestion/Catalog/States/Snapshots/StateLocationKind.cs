namespace LoDb.Ingestion.Catalog.States.Snapshots;

/// <summary>Where a page's version stands to the index.</summary>
public enum StateLocationKind
{
    /// <summary>
    /// A covered version: the state is the entity's state there, or none when the entity has
    /// no page at that version.
    /// </summary>
    Covered,

    /// <summary>
    /// A version after the coverage end, up to the latest one: the index lags a promotion, and
    /// the entity's open state stands for it (short URL, class revisable).
    /// </summary>
    Lagging,

    /// <summary>
    /// No state applies: a version older than the coverage, not covered, listed after the
    /// latest one, before any promotion, or an empty index. The residual rule decides.
    /// </summary>
    Outside,
}
