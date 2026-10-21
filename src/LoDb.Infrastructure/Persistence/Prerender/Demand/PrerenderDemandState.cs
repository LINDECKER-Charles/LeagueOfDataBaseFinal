namespace LoDb.Infrastructure.Persistence.Prerender.Demand;

/// <summary>
/// Where a page of the automatic queue is, stored as <c>pending</c>, <c>rendering</c>,
/// <c>done</c>, <c>failed</c> or <c>skipped</c>.
/// </summary>
public enum PrerenderDemandState
{
    /// <summary>Waits for the queue's worker, the most requested first.</summary>
    Pending,

    /// <summary>The worker renders it now; a restart puts it back to pending.</summary>
    Rendering,

    /// <summary>Written by the queue into the epoch of its row.</summary>
    Done,

    /// <summary>Failed every try; kept for the admin until it is pruned.</summary>
    Failed,

    /// <summary>
    /// Not to render in the epoch of its row: a pass stored it already, or the SSR answered a
    /// page that is never stored (a redirect, a 404).
    /// </summary>
    Skipped,
}
