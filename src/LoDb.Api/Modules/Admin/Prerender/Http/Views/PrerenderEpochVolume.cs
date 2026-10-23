using LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>The pages of one epoch of the store, and what keeping them costs each month.</summary>
internal sealed record PrerenderEpochVolume
{
    public required int Epoch { get; init; }

    /// <summary>served, writing, standby (written, not served) or retired.</summary>
    public required string State { get; init; }

    /// <summary>Whether the active pass writes it: the served epoch while it fills, say.</summary>
    public required bool IsWriting { get; init; }

    /// <summary>Pages of the passes and of the automatic queue.</summary>
    public required long Pages { get; init; }

    /// <summary>Bytes of the passes and of the automatic queue.</summary>
    public required long Bytes { get; init; }

    /// <summary>
    /// Of <see cref="Pages"/>, those the automatic queue wrote and no pass wrote again since.
    /// </summary>
    public required long QueuePages { get; init; }

    /// <summary>Of <see cref="Bytes"/>, those of <see cref="QueuePages"/>.</summary>
    public required long QueueBytes { get; init; }

    /// <summary>The bytes in GB times the One Zone price, to four decimals.</summary>
    public required decimal MonthlyCost { get; init; }

    /// <summary>
    /// How much of the older patches it holds, (version, locale) by (version, locale); null for
    /// a retired epoch.
    /// </summary>
    public PrerenderEpochCoverage? Coverage { get; init; }
}
