namespace LoDb.Infrastructure.Persistence.Scheduling;

/// <summary>
/// A row of <c>periodic_job</c>: when a periodic job last started and last succeeded, on
/// any instance.
/// </summary>
/// <remarks>
/// The advisory lock keeps two runs from overlapping; this row keeps a job from running
/// twice in the same period when instances tick at different times, or after a restart.
/// </remarks>
public sealed class PeriodicJobState
{
    public required string Name { get; set; }

    public DateTimeOffset LastStartedAt { get; set; }

    public DateTimeOffset? LastSucceededAt { get; set; }
}
