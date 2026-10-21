namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;

/// <summary>
/// What the instance answering sees of the engine: none of it is stored, so another instance
/// sees nothing (ObservedAt null).
/// </summary>
internal sealed record PrerenderEnginePulse
{
    /// <summary>When this instance last ran the engine; null when it never did.</summary>
    public DateTimeOffset? ObservedAt { get; init; }

    /// <summary>Whether this instance runs its background tasks (LoDb:Workers:Enabled).</summary>
    public required bool IsWorkerEnabled { get; init; }

    /// <summary>Since when the SSR is out of reach for the pass; null while it answers.</summary>
    public DateTimeOffset? SsrUnreachableSince { get; init; }

    /// <summary>
    /// The pace while this instance drains this pass; null otherwise (another instance writes
    /// it, or none, or another pass).
    /// </summary>
    public PrerenderPaceView? Pace { get; init; }

    /// <summary>The versions of this pass whose units wait.</summary>
    public required IReadOnlyList<PrerenderWaitView> Waiting { get; init; }
}
