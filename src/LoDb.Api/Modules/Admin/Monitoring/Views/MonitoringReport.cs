namespace LoDb.Api.Modules.Admin.Monitoring.Views;

/// <summary>
/// The state of the running API: its dependencies, its queues, the Data Dragon versions,
/// the application counters and the weight of the main tables.
/// </summary>
/// <remarks>
/// Every section degrades on its own: a database down leaves the probes and the process
/// figures readable, with <see cref="Counters"/> and <see cref="Versions"/> null.
/// </remarks>
internal sealed record MonitoringReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    public required IReadOnlyList<ServiceProbe> Services { get; init; }

    /// <summary>Null when the database could not be read.</summary>
    public AppCounters? Counters { get; init; }

    public required IngestionState Ingestion { get; init; }

    /// <summary>Null when the database could not be read.</summary>
    public VersionState? Versions { get; init; }

    public required ProcessFigures Process { get; init; }

    /// <summary>Weight of the main tables, the heaviest first; empty when unreadable.</summary>
    public required IReadOnlyList<TableVolume> Tables { get; init; }
}
