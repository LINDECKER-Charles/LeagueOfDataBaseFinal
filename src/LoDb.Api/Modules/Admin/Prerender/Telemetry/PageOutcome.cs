namespace LoDb.Api.Modules.Admin.Prerender.Telemetry;

/// <summary>
/// The outcomes <c>lodb_prerender_pages_total</c> counts, its <c>outcome</c> label
/// (<c>docs/guides/metriques.md</c>).
/// </summary>
internal enum PageOutcome
{
    /// <summary>The page was written to the store.</summary>
    Written,

    /// <summary>The render is not one the store keeps: a 301, a 404, another class.</summary>
    Skipped,

    /// <summary>The SSR had no room for it: tried again after its Retry-After.</summary>
    Refused,

    /// <summary>The page was given up after its tries.</summary>
    Failed,
}
