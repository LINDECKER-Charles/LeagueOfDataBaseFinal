namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;

/// <summary>What the admin estimates a pass with, all measured here.</summary>
internal sealed record PrerenderEstimateBasis
{
    /// <summary>
    /// The share of each type in a version's pages, from the catalog of the newest older patch
    /// a unit completed (its catalog is stored); null when none did.
    /// </summary>
    public PrerenderTypeShares? TypeShares { get; init; }

    /// <summary>
    /// Mean bytes of a written page over the completed units of the served epoch.
    /// </summary>
    public long? BytesPerPage { get; init; }

    /// <summary>
    /// Pages per minute of the latest pass that ran 10 minutes or more; null if none. The
    /// client bounds it by ConfiguredRendersPerMinute: the ceiling may have been lowered since.
    /// </summary>
    public decimal? MeasuredPagesPerMinute { get; init; }

    /// <summary>The pass's ceiling, LoDb:Prerender:RendersPerMinute.</summary>
    public required int ConfiguredRendersPerMinute { get; init; }

    public required decimal PricePerGbMonthOneZone { get; init; }

    /// <summary>ISO 4217 code of the price.</summary>
    public required string Currency { get; init; }
}
