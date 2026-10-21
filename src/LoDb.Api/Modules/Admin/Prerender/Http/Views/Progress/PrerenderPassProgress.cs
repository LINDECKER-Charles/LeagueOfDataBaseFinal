namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;

/// <summary>
/// The progress of a pass, sized to be read every few seconds: its counters, each version's
/// cells, what runs, what comes next, what just ended or failed, and the engine's own signals.
/// </summary>
internal sealed record PrerenderPassProgress
{
    public required DateTimeOffset GeneratedAt { get; init; }

    public required PrerenderPassView Pass { get; init; }

    /// <summary>
    /// The locales the pass has units in, in the site's order: the order of each Cells.
    /// </summary>
    public required IReadOnlyList<string> Locales { get; init; }

    /// <summary>
    /// Whether Versions holds every version of the pass; false when it holds only those with a
    /// unit changed since the <c>since</c> asked for.
    /// </summary>
    public required bool IsComplete { get; init; }

    /// <summary>The pass's versions, in the order the worker reaches them.</summary>
    public required IReadOnlyList<PrerenderVersionProgress> Versions { get; init; }

    /// <summary>
    /// The older patches the pass's scope lists, in the order sent, when IsComplete and the
    /// scope lists some; null otherwise (every older patch, or an incremental read). The list
    /// views carry only their count (PrerenderScopeView).
    /// </summary>
    public IReadOnlyList<string>? ScopeVersions { get; init; }

    /// <summary>The units running now; one in practice (left running by a pause, see z).</summary>
    public required IReadOnlyList<PrerenderUnitView> Running { get; init; }

    /// <summary>
    /// The next pending units, in the order the worker takes them, without the versions this
    /// instance sees waiting (Pulse.Waiting); 5 at most.
    /// </summary>
    public required IReadOnlyList<PrerenderUnitKey> Next { get; init; }

    /// <summary>The units that ended last, completed or failed, newest first; 12 at most.</summary>
    public required IReadOnlyList<PrerenderUnitView> Recent { get; init; }

    /// <summary>The units failed or with failed pages, newest first; 20 at most.</summary>
    public required IReadOnlyList<PrerenderUnitView> Failures { get; init; }

    /// <summary>Every unit failed or with failed pages.</summary>
    public required int UnitsWithFailures { get; init; }

    public required PrerenderEnginePulse Pulse { get; init; }
}
