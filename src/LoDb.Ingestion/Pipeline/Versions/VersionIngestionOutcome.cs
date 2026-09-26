namespace LoDb.Ingestion.Pipeline.Versions;

/// <summary>How an ingestion run of a version ended.</summary>
public enum VersionIngestionOutcome
{
    /// <summary>Every dataset is stored and every image has a verdict.</summary>
    Completed,

    /// <summary>
    /// An upstream failure left something out; nothing half-read is stored and the version is
    /// due again later.
    /// </summary>
    Incomplete,

    /// <summary>Another run, here or on another instance, holds the version.</summary>
    Locked,

    /// <summary>Data Dragon does not list the version or one of the languages.</summary>
    Unknown,
}
