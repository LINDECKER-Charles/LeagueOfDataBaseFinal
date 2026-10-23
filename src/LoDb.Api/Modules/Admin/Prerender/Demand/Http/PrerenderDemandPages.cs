namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>A page of the list of the automatic queue's pages in one state.</summary>
internal sealed record PrerenderDemandPages
{
    /// <summary>The state listed: <c>pending</c>, <c>rendering</c>, <c>done</c>…</summary>
    public required string State { get; init; }

    /// <summary>The page of the list, from 1.</summary>
    public required int Page { get; init; }

    public required int PageSize { get; init; }

    /// <summary>Pages of the queue in that state, all pages of the list together.</summary>
    public required long Total { get; init; }

    /// <summary>
    /// Pending ones in the worker's order (the most requested, then the oldest), done ones the
    /// last written first, the others the last changed first.
    /// </summary>
    public required IReadOnlyList<PrerenderDemandListedPage> Pages { get; init; }
}
