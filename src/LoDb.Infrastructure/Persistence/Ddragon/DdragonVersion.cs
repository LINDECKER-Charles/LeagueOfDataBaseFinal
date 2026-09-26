namespace LoDb.Infrastructure.Persistence.Ddragon;

/// <summary>
/// A row of <c>ddragon_version</c>: the ingestion state of one Data Dragon version.
/// </summary>
public sealed class DdragonVersion
{
    public required string Version { get; set; }

    public DdragonVersionStatus Status { get; set; }

    /// <summary>Ingestion attempts made so far, bounded by the pipeline.</summary>
    public int Attempts { get; set; }

    /// <summary>Earliest time of the next attempt after a transient error.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    public DateTimeOffset DiscoveredAt { get; set; }

    /// <summary>Time of the last change of the row.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }

    /// <summary>When the version became the latest one served by the site and the apps.</summary>
    public DateTimeOffset? PromotedAt { get; set; }
}
