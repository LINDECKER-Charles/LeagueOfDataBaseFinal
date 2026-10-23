namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>A page of the automatic queue, whatever its state, as the admin lists them.</summary>
internal sealed record PrerenderDemandListedPage
{
    /// <summary>The page on the site: <c>/fr/15.13.1/items/1036-long-sword</c>.</summary>
    public required string Path { get; init; }

    public required string Version { get; init; }

    /// <summary>The locale as the URLs spell it: <c>fr</c>, <c>zh-hans</c>.</summary>
    public required string Locale { get; init; }

    /// <summary>The first segment of the page: <c>champions</c>, <c>changes</c>…</summary>
    public required string PageType { get; init; }

    /// <summary>
    /// <c>pending</c>, <c>rendering</c>, <c>done</c>, <c>failed</c> or <c>skipped</c>.
    /// </summary>
    public required string State { get; init; }

    /// <summary>Refusals of it the SSR counted since it entered the queue.</summary>
    public required long Demand { get; init; }

    /// <summary>Failed tries so far.</summary>
    public required int Attempts { get; init; }

    public required DateTimeOffset FirstSeenAt { get; init; }

    /// <summary>When a visitor was last refused it.</summary>
    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>When the queue last wrote it; null until then.</summary>
    public DateTimeOffset? RenderedAt { get; init; }

    /// <summary>The epoch it was written to or found in; null while pending.</summary>
    public int? Epoch { get; init; }

    /// <summary>Bytes written, for a done page; null otherwise or when not kept.</summary>
    public long? Bytes { get; init; }

    /// <summary><c>archived</c> or <c>revisable</c>, for a done page; null otherwise.</summary>
    public string? StoredClass { get; init; }

    /// <summary>
    /// When a pass wrote it again in its epoch; null while the queue's copy stands.
    /// </summary>
    public DateTimeOffset? SupersededAt { get; init; }

    /// <summary>Why it failed, was skipped or waits; null otherwise.</summary>
    public string? Reason { get; init; }
}
