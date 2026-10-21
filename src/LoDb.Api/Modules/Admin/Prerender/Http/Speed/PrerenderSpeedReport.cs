using LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Speed;

/// <summary>
/// The speed of the pre-rendering as the admin sets and watches it: the operator's setting,
/// what the environment allows, the pace in force and the host's resources.
/// </summary>
internal sealed record PrerenderSpeedReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Whether the pass and its store are configured on this host.</summary>
    public required bool IsConfigured { get; init; }

    /// <summary>
    /// Whether the environment's kill switch holds the fixed pace
    /// (<c>LoDb:Prerender:Pace:Mode=fixed</c>): the setting then waits, unused.
    /// </summary>
    public required bool IsFixed { get; init; }

    public required PrerenderSpeedSettingView Setting { get; init; }

    public required PrerenderSpeedLimits Limits { get; init; }

    /// <summary>The pace in force while this instance drains a pass; null otherwise.</summary>
    public PrerenderPaceView? Pace { get; init; }

    /// <summary>The host's resources; null before the first read (30 s after the start).</summary>
    public PrerenderResourcesView? Resources { get; init; }
}
