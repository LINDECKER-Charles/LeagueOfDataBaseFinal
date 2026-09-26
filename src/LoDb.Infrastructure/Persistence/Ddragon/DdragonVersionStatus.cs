namespace LoDb.Infrastructure.Persistence.Ddragon;

/// <summary>
/// Ingestion state of a version, stored as <c>discovered</c>, <c>ingesting</c>,
/// <c>ready</c> or <c>failed</c>.
/// </summary>
public enum DdragonVersionStatus
{
    /// <summary>Seen in <c>versions.json</c>, waiting for its ingestion.</summary>
    Discovered,

    /// <summary>Datasets and images are being ingested.</summary>
    Ingesting,

    /// <summary>Fully ingested: the version may be promoted.</summary>
    Ready,

    /// <summary>Gave up after the maximum number of attempts.</summary>
    Failed,
}
