using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Infrastructure.Persistence.Prerender.Demand;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Drain;

/// <summary>
/// What one batch of the queue did, for its summary line; shared by its pages in flight.
/// </summary>
internal sealed class DemandTally
{
    private readonly Lock _gate = new();

    public int Pages { get; private set; }

    public int Written { get; private set; }

    public int Skipped { get; private set; }

    public int Failed { get; private set; }

    public int Deferred { get; private set; }

    public long Bytes { get; private set; }

    public int Refusals { get; private set; }

    public string? LastReason { get; private set; }

    /// <summary>Why the batch stopped short, when a page stopped it.</summary>
    public string? StoppedBy { get; private set; }

    public bool IsStopped
    {
        get
        {
            lock (_gate)
            {
                return StoppedBy is not null;
            }
        }
    }

    public void Add(DemandEnd end)
    {
        ArgumentNullException.ThrowIfNull(end);
        lock (_gate)
        {
            Pages++;
            Bytes += end.Bytes;
            Refusals += end.Refusals;
            LastReason = end.Reason ?? LastReason;
            StoppedBy ??= end.StopsDrain ? end.Reason : null;
            CountState(end);
        }
    }

    private void CountState(DemandEnd end)
    {
        if (end.State == PrerenderDemandState.Done)
        {
            Written++;
        }
        else if (end.State == PrerenderDemandState.Skipped)
        {
            Skipped++;
        }
        else if (end.IsFailedTry)
        {
            Failed++;
        }
        else
        {
            Deferred++;
        }
    }
}
