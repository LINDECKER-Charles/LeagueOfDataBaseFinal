namespace LoDb.Infrastructure.Persistence.Prerender.Demand;

/// <summary>
/// A row of <c>prerender_demand</c>: a page of an older patch the visitors' SSR refused, how
/// often, and where the automatic queue stands with it (docs/produit/pre-rendu-catalogue-cdn.md,
/// « File automatique »).
/// </summary>
/// <remarks>
/// Keyed by the page's canonical path. <see cref="SsrInstance"/> and <see cref="SsrCount"/>
/// are the last cumulative count the SSR gave for it: the API adds only the increase, so that
/// a read made twice, a second reader or a restart of either side never counts a refusal twice.
/// </remarks>
public sealed class PrerenderDemandPage
{
    /// <summary>Far over the longest page of the catalogue, as the SSR bounds it.</summary>
    public const int PathMaxLength = 160;

    /// <summary>The longest type segment (<c>champions</c>, <c>summoners</c>).</summary>
    public const int PageTypeMaxLength = 16;

    /// <summary>The SSR's instance, a UUID.</summary>
    public const int InstanceMaxLength = 64;

    /// <summary>The longest reason kept; a longer one is cut.</summary>
    public const int ReasonMaxLength = 300;

    /// <summary>The page on the site: <c>/fr/15.13.1/items/1036-long-sword</c>.</summary>
    public required string Path { get; set; }

    public required string Version { get; set; }

    /// <summary>The locale as the URLs spell it: <c>fr</c>, <c>zh-hans</c>.</summary>
    public required string Locale { get; set; }

    /// <summary>The first segment of the page: <c>champions</c>, <c>items</c>…</summary>
    public required string PageType { get; set; }

    /// <summary>Refusals counted since the page entered the queue.</summary>
    public long Demand { get; set; }

    public string? SsrInstance { get; set; }

    public long SsrCount { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    /// <summary>When its count last grew.</summary>
    public DateTimeOffset LastSeenAt { get; set; }

    public required PrerenderDemandState State { get; set; }

    /// <summary>Tries that failed; a page is failed for good past the queue's limit.</summary>
    public int Attempts { get; set; }

    /// <summary>
    /// The epoch the page was written to or found in (done, skipped); null while pending. A
    /// row of an epoch no longer served goes back to pending.
    /// </summary>
    public int? Epoch { get; set; }

    /// <summary>When the queue wrote it; null until then.</summary>
    public DateTimeOffset? RenderedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Why it failed or was skipped; null otherwise.</summary>
    public string? Reason { get; set; }
}
