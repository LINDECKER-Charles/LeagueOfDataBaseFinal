namespace LoDb.Api.Modules.Admin.Prerender.Revision.Http;

/// <summary>
/// The queue of the closed states as the admin shows it: whether it runs, what it holds by
/// epoch, what it wrote lately, and how far its producer went through the closures of the
/// index.
/// </summary>
internal sealed record PrerenderRevisionReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Whether it renders: the store is configured. It never depends on
    /// <c>AutoEnqueueOnPromotion</c>.
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>The epoch nginx serves.</summary>
    public required int ServedEpoch { get; init; }

    /// <summary>Pages waiting, due or not.</summary>
    public required long Pending { get; init; }

    /// <summary>
    /// Pending pages due now: the servers read an index that covers their closure.
    /// </summary>
    public required long Due { get; init; }

    public required long Done { get; init; }

    public required long Failed { get; init; }

    public required long Skipped { get; init; }

    /// <summary>Pages it wrote in the last hour.</summary>
    public required long WrittenLastHour { get; init; }

    /// <summary>Pages it wrote in the last 24 hours.</summary>
    public required long WrittenLastDay { get; init; }

    /// <summary>When the next pending page is due; null when none waits.</summary>
    public DateTimeOffset? NextDueAt { get; init; }

    /// <summary>
    /// The <c>closed_at</c> of the last closure its producer enqueued; null before its first
    /// run.
    /// </summary>
    public DateTimeOffset? ClosedThrough { get; init; }

    /// <summary>What it holds of each epoch, the newest first.</summary>
    public required IReadOnlyList<PrerenderRevisionEpoch> Epochs { get; init; }
}
