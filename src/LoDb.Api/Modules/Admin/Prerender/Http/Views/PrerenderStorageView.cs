namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>
/// What the page store holds, read from the counters the passes keep as they write (the
/// writer key cannot list), and what storing it is estimated to cost each month.
/// </summary>
/// <remarks>
/// An estimate: the egress is left out (<see cref="ExcludesEgress"/>), and on the staging,
/// whose pages expire after 14 days, the counters say what was written rather than what is
/// left. A retired epoch keeps costing until the store has deleted it, which the API cannot
/// see: its row shows its cost, the totals leave it out.
/// </remarks>
internal sealed record PrerenderStorageView
{
    /// <summary>Each epoch holding pages, plus the served and written ones, newest first.</summary>
    public required IReadOnlyList<PrerenderEpochVolume> Epochs { get; init; }

    public required PrerenderBuildVolume Builds { get; init; }

    /// <summary>The pages of the served epoch, version by version, newest first.</summary>
    public required IReadOnlyList<PrerenderVersionVolume> ServedVersions { get; init; }

    /// <summary>Pages of every epoch but the retired ones.</summary>
    public required long Pages { get; init; }

    /// <summary>Bytes of every epoch but the retired ones, and of the builds.</summary>
    public required long Bytes { get; init; }

    /// <summary>The cost of those epochs and of the builds, to four decimals.</summary>
    public required decimal MonthlyCost { get; init; }

    /// <summary>Price of one GB (10^9 bytes) of pages, in One Zone, for a month.</summary>
    public required decimal PricePerGbMonthOneZone { get; init; }

    /// <summary>Price of one GB (10^9 bytes) of bundles, in Multi-AZ, for a month.</summary>
    public required decimal PricePerGbMonthMultiAz { get; init; }

    /// <summary>ISO 4217 code of the prices.</summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Always true: the downloads (75 GB a month free, then billed per GB) are not in the
    /// estimate; Grafana follows them.
    /// </summary>
    public required bool ExcludesEgress { get; init; }
}
