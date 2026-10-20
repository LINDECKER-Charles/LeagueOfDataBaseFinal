namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;

/// <summary>The units of one version in a pass, a row per locale.</summary>
internal sealed record PrerenderVersionUnits
{
    public required long PassId { get; init; }

    public required string Version { get; init; }

    /// <summary>
    /// Its units, in the site's order of the locales; empty when the pass has none.
    /// </summary>
    public required IReadOnlyList<PrerenderUnitView> Units { get; init; }
}
