namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>The units of a pass, one per (version, locale), by state.</summary>
internal sealed record PrerenderUnitCounts
{
    public required int Total { get; init; }

    public required int Pending { get; init; }

    public required int Running { get; init; }

    public required int Completed { get; init; }

    public required int Failed { get; init; }
}
