namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>
/// The front builds copied to the store, every hashed file of each, and what keeping them
/// costs each month.
/// </summary>
internal sealed record PrerenderBuildVolume
{
    public required int Count { get; init; }

    public required long Files { get; init; }

    public required long Bytes { get; init; }

    /// <summary>The bytes in GB times the Multi-AZ price, to four decimals.</summary>
    public required decimal MonthlyCost { get; init; }
}
