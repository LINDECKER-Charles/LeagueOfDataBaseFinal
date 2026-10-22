namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>
/// The automatic queue of the pre-rendering as the admin shows it: whether it runs, what it
/// holds, what it wrote lately, and its most requested pending pages.
/// </summary>
internal sealed record PrerenderDemandReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>The kill switch, <c>LoDb:Prerender:Demand:Enabled</c>.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Whether it renders: switched on and the store configured.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Whether it reads the SSR's refused pages: active, and their URL set.</summary>
    public required bool IsCapturing { get; init; }

    /// <summary>Its share of the pass SSR's renders while a manual pass runs, in percent.</summary>
    public required int QueueSharePercent { get; init; }

    /// <summary>The epoch it writes: the served one.</summary>
    public required int Epoch { get; init; }

    public required long Pending { get; init; }

    public required long Done { get; init; }

    public required long Failed { get; init; }

    public required long Skipped { get; init; }

    /// <summary>Pages it wrote in the last hour.</summary>
    public required long WrittenLastHour { get; init; }

    /// <summary>Pages it wrote in the last 24 hours.</summary>
    public required long WrittenLastDay { get; init; }

    /// <summary>The ten most requested pending pages, the most requested first.</summary>
    public required IReadOnlyList<PrerenderDemandPage> Top { get; init; }
}
