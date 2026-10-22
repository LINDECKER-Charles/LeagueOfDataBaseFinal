namespace LoDb.Api.Modules.Admin.Prerender.Demand;

/// <summary>
/// The automatic queue of the pre-rendering (<c>LoDb:Prerender:Demand</c>): the pages of
/// older patches the visitors' SSR refused, rendered the most requested first into the served
/// epoch, beside the manual passes and within their pace (docs/produit/pre-rendu-catalogue-cdn.md,
/// « File automatique des pages refusées »).
/// </summary>
internal sealed class DemandOptions
{
    public const string SectionName = "LoDb:Prerender:Demand";

    /// <summary>The queue's share of the renders while a pass runs, by default.</summary>
    public const int DefaultQueueSharePercent = 50;

    /// <summary>How long a page no longer refused stays in the table, by default.</summary>
    public const int DefaultRetentionDays = 30;

    /// <summary>
    /// Pending pages kept at most, the most requested: some thousand times what the queue
    /// renders in a day at the pass's floor, and a few tens of megabytes of table.
    /// </summary>
    public const int DefaultMaxPendingPages = 100_000;

    /// <summary>Failed tries before a page is failed for good, by default.</summary>
    public const int DefaultMaxAttempts = 3;

    /// <summary>
    /// The kill switch: false, the queue neither reads the SSR nor renders, and the panel says
    /// so. It never runs either while the store is not configured.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Where the visitors' SSR lists the pages it refused (<c>http://lodb-web-ssr:9464/refused</c>,
    /// its metrics port, never published); unset, nothing new enters the queue.
    /// </summary>
    public string? RefusedPagesUrl { get; set; }

    /// <summary>
    /// The queue's share of the pass SSR's renders in flight while a manual pass runs (1 to 100);
    /// all of them when none runs. Neither starves: the pass keeps the rest, and each waits its
    /// turn behind the other.
    /// </summary>
    public int QueueSharePercent { get; set; } = DefaultQueueSharePercent;

    /// <summary>Days a page stays in the table once no visitor was refused it.</summary>
    public int RetentionDays { get; set; } = DefaultRetentionDays;

    /// <summary>Pending pages kept at most: past it, the least requested go.</summary>
    public int MaxPendingPages { get; set; } = DefaultMaxPendingPages;

    /// <summary>Failed tries before a page is failed for good.</summary>
    public int MaxAttempts { get; set; } = DefaultMaxAttempts;
}
