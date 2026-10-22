using LoDb.Infrastructure.Persistence.Prerender.Demand;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Queue;

/// <summary>
/// How the queue's worker left one page: the state its row takes, why, and whether the drain
/// stops there.
/// </summary>
internal sealed record DemandEnd
{
    public required PrerenderDemandState State { get; init; }

    /// <summary>Why it failed, was skipped or waits; null for a written page.</summary>
    public string? Reason { get; init; }

    /// <summary>Bytes written to the store, for a written page.</summary>
    public long Bytes { get; init; }

    /// <summary>Whether it counts a failed try against the page.</summary>
    public bool IsFailedTry { get; init; }

    /// <summary>
    /// Whether every page would end alike now (the SSR out of reach, a setting that refuses
    /// them all): the drain stops until its next tick, the page pending.
    /// </summary>
    public bool StopsDrain { get; init; }

    /// <summary>The SSR's refusals waited out on the way.</summary>
    public int Refusals { get; init; }

    /// <summary>
    /// The class the SSR marked a written page with (<c>archived</c>, <c>revisable</c>); null
    /// for a page not written.
    /// </summary>
    public string? StoredClass { get; init; }

    public static DemandEnd Written(long bytes, string storedClass) => new()
    {
        State = PrerenderDemandState.Done,
        Bytes = bytes,
        StoredClass = storedClass,
    };

    public static DemandEnd Skipped(string reason) =>
        new() { State = PrerenderDemandState.Skipped, Reason = reason };

    /// <summary>A failed try: failed for good at the last one, pending again before.</summary>
    public static DemandEnd Failed(string reason, bool isLast) => new()
    {
        State = isLast ? PrerenderDemandState.Failed : PrerenderDemandState.Pending,
        Reason = reason,
        IsFailedTry = true,
    };

    /// <summary>Not rendered yet, for a reason that will pass: pending, no try counted.</summary>
    public static DemandEnd Deferred(string reason) =>
        new() { State = PrerenderDemandState.Pending, Reason = reason };

    /// <summary>Pending again, and the drain stops (<see cref="StopsDrain"/>).</summary>
    public static DemandEnd Stopped(string reason) =>
        new() { State = PrerenderDemandState.Pending, Reason = reason, StopsDrain = true };
}
