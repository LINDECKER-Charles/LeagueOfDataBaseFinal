namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;

/// <summary>
/// How much of the older patches an epoch holds, (version, locale) by (version, locale).
/// </summary>
internal sealed record PrerenderEpochCoverage
{
    /// <summary>The older patches Data Dragon lists now.</summary>
    public required int PinnedVersions { get; init; }

    /// <summary>Their (version, locale): PinnedVersions times the site's 21 locales.</summary>
    public required int ExpectedUnits { get; init; }

    /// <summary>(version, locale) covered over every type of page (c or w).</summary>
    public required int CompleteUnits { get; init; }

    /// <summary>Those of CompleteUnits holding every type (c).</summary>
    public required int CleanUnits { get; init; }

    /// <summary>(version, locale) whose unit failed for good (f): the SSR serves them.</summary>
    public required int FailedUnits { get; init; }

    /// <summary>
    /// (version, locale) with a gap, absent (-) or partial (p): what keeps the epoch from being
    /// ready, ExpectedUnits − CompleteUnits − FailedUnits.
    /// </summary>
    public required int GapUnits { get; init; }

    /// <summary>Pages failed in the c and w cells, which the SSR serves once switched.</summary>
    public required long FailedPages { get; init; }

    /// <summary>Older patches complete (c or w) in all 21 locales.</summary>
    public required int CompleteVersions { get; init; }

    /// <summary>Older patches with a gap in one locale at least.</summary>
    public required int GapVersions { get; init; }
}
