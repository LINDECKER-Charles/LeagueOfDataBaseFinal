namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;

/// <summary>
/// The older patches and what one epoch holds of each: the admin's coverage view.
/// </summary>
internal sealed record PrerenderPatchesReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>The epoch the coverage is read in.</summary>
    public required int Epoch { get; init; }

    public required int ServedEpoch { get; init; }

    /// <summary>
    /// The latest version, which the SSR serves and no pass renders; null before any.
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>The site's locales, in the order of each patch's cells.</summary>
    public required IReadOnlyList<string> Locales { get; init; }

    /// <summary>Every older patch, newest first.</summary>
    public required IReadOnlyList<PrerenderPatchRow> Patches { get; init; }

    public required PrerenderEpochCoverage Coverage { get; init; }

    public required PrerenderEstimateBasis Estimates { get; init; }
}
