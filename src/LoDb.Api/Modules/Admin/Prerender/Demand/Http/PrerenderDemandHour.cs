namespace LoDb.Api.Modules.Admin.Prerender.Demand.Http;

/// <summary>One hour of the automatic queue: the pages that entered it, those it wrote.</summary>
internal sealed record PrerenderDemandHour
{
    /// <summary>The start of the hour, in UTC.</summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>Pages first refused in that hour.</summary>
    public required long Entered { get; init; }

    /// <summary>Pages the queue wrote in that hour, whatever became of them since.</summary>
    public required long Written { get; init; }
}
