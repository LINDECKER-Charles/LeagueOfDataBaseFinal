namespace LoDb.Ingestion.Catalog.States.Updates;

/// <summary>What asks the index of states to catch up: it decides what the update may do.</summary>
public enum StateIndexTrigger
{
    /// <summary>
    /// The <c>catalog states build</c> command, before the API serves: it may rebuild the
    /// index into a new generation, and downloads nothing unless told to.
    /// </summary>
    Build,

    /// <summary>
    /// The promotion of a version: the index extends to it, listed version by listed version,
    /// downloading the en_US datasets an intermediate version lacks.
    /// </summary>
    Promotion,

    /// <summary>The periodic reconciliation, which extends the index as a promotion does.</summary>
    Reconcile,
}
