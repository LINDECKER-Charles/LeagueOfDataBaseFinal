namespace LoDb.Ingestion.Catalog.States.Updates;

/// <summary>What an update of the index of states did.</summary>
public enum StateIndexOutcome
{
    /// <summary>
    /// Nothing: the current generation already covered every version up to the latest one,
    /// or no version is promoted yet.
    /// </summary>
    Unchanged,

    /// <summary>The current generation was extended to the latest version.</summary>
    Extended,

    /// <summary>A new generation was built, then made current instead of the old one.</summary>
    Built,

    /// <summary>
    /// Another writer held the index's lock for longer than the update waits: nothing was
    /// done, the holder and the reconciliation finish the work.
    /// </summary>
    Locked,
}
