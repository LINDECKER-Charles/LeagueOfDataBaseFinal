namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>The pages of one epoch of the store, and what keeping them costs each month.</summary>
internal sealed record PrerenderEpochVolume
{
    public required int Epoch { get; init; }

    /// <summary>served, writing, standby (written, not served) or retired.</summary>
    public required string State { get; init; }

    /// <summary>Whether the active pass writes it: the served epoch while it fills, say.</summary>
    public required bool IsWriting { get; init; }

    public required long Pages { get; init; }

    public required long Bytes { get; init; }

    /// <summary>The bytes in GB times the One Zone price, to four decimals.</summary>
    public required decimal MonthlyCost { get; init; }
}
