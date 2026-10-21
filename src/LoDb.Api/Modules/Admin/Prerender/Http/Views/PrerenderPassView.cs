namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>A pass of the pre-rendering, its progress, its pace and when it should end.</summary>
internal sealed record PrerenderPassView
{
    public required long Id { get; init; }

    /// <summary>The epoch it writes.</summary>
    public required int Epoch { get; init; }

    /// <summary>running, paused, completed or cancelled.</summary>
    public required string State { get; init; }

    public required PrerenderScopeView Scope { get; init; }

    /// <summary>The administrator who launched it; null for a promotion or the CLI.</summary>
    public int? AuthorId { get; init; }

    /// <summary>Their username, null too once their account is deleted.</summary>
    public string? AuthorName { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>Seconds spent running, the pauses left out, up to the time of the report.</summary>
    public required long ActiveSeconds { get; init; }

    /// <summary>
    /// Pages of the units listed so far: a unit's pages are listed when the pass reaches it,
    /// so the total grows as the pass goes.
    /// </summary>
    public required long PagesTotal { get; init; }

    public required long PagesWritten { get; init; }

    /// <summary>Pages the SSR did not render as an older patch's (a redirect, a 404).</summary>
    public required long PagesSkipped { get; init; }

    public required long PagesFailed { get; init; }

    public required long BytesWritten { get; init; }

    public required PrerenderUnitCounts Units { get; init; }

    /// <summary>
    /// Pages written, skipped or failed per minute of running time, to one decimal; null
    /// before a minute of it.
    /// </summary>
    public decimal? PagesPerMinute { get; init; }

    /// <summary>
    /// When it should end at its pace, once its pages left are processed; null unless it runs
    /// with a known pace and some units listed.
    /// </summary>
    public DateTimeOffset? EstimatedEndAt { get; init; }

    /// <summary>
    /// Since when it runs, from its launch or its last resume; null unless running. A resume
    /// writes no unit: without it, a pass resumed after a long pause would look stalled.
    /// </summary>
    public DateTimeOffset? RunningSince { get; init; }

    /// <summary>Since when it is paused; null unless paused.</summary>
    public DateTimeOffset? PausedAt { get; init; }

    /// <summary>
    /// Why the pass paused itself (the store or the SSR refusing it), as the engine says it;
    /// null for a pause of an administrator, and once it runs again.
    /// </summary>
    public string? PauseReason { get; init; }

    /// <summary>
    /// Pages still to process: those of the listed units not processed yet, plus each unit not
    /// listed yet at its version's expected pages, else at the mean of the listed ones. Null
    /// before any unit is listed.
    /// </summary>
    public long? PagesLeft { get; init; }
}
