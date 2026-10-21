namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>A pending page of the automatic queue, as the admin lists the most requested.</summary>
internal sealed record PrerenderDemandPage
{
    /// <summary>The page on the site: <c>/fr/15.13.1/items/1036-long-sword</c>.</summary>
    public required string Path { get; init; }

    public required string Version { get; init; }

    /// <summary>The locale as the URLs spell it: <c>fr</c>, <c>zh-hans</c>.</summary>
    public required string Locale { get; init; }

    /// <summary>Refusals of it the SSR counted since it entered the queue.</summary>
    public required long Demand { get; init; }

    /// <summary>Failed tries so far.</summary>
    public required int Attempts { get; init; }

    /// <summary>When a visitor was last refused it.</summary>
    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>Why it waits, after a try or a check; null for a page not tried yet.</summary>
    public string? Reason { get; init; }
}
