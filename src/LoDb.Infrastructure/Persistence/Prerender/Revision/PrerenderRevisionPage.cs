using LoDb.Infrastructure.Persistence.Prerender.Demand;

namespace LoDb.Infrastructure.Persistence.Prerender.Revision;

/// <summary>
/// A row of <c>prerender_revision</c>: a stored page of an older patch to render again in one
/// epoch, because the promotion that closed its entity's state changed it
/// (docs/produit/seo-une-page-par-etat.md, § 3.7).
/// </summary>
/// <remarks>
/// Keyed by the page and the epoch: the producer inserts a page for the served epoch and for
/// the epoch an active pass writes, idempotently. Its states are those of the automatic queue
/// (<see cref="PrerenderDemandState"/>), with the same meaning.
/// </remarks>
public sealed class PrerenderRevisionPage
{
    /// <summary>The page on the site: <c>/fr/15.13.1/items/1036-long-sword</c>.</summary>
    public required string Path { get; set; }

    public int Epoch { get; set; }

    public required string Version { get; set; }

    /// <summary>The locale as the URLs spell it: <c>fr</c>, <c>zh-hans</c>.</summary>
    public required string Locale { get; set; }

    public required PrerenderDemandState State { get; set; }

    /// <summary>Tries that failed; a page is failed for good past the queue's limit.</summary>
    public int Attempts { get; set; }

    public DateTimeOffset EnqueuedAt { get; set; }

    /// <summary>
    /// The earliest the worker renders it: once the serving API reads an index that covers the
    /// closure, which a settling delay after the promotion stands for.
    /// </summary>
    public DateTimeOffset DueAt { get; set; }

    /// <summary>When the worker wrote it; null until then.</summary>
    public DateTimeOffset? WrittenAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Why it failed or was skipped; null otherwise.</summary>
    public string? Reason { get; set; }
}
