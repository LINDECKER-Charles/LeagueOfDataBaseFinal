namespace LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

/// <summary>What the regulator remembers between two decisions of a drain.</summary>
internal sealed record PaceMemory
{
    public required PaceState State { get; init; }

    /// <summary>Renders the pass may start per minute: 0 while held.</summary>
    public required int Allowance { get; init; }

    public PaceReason Reason { get; init; }

    /// <summary>When the state began.</summary>
    public required DateTimeOffset Since { get; init; }

    /// <summary>When the drain began.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>The last increase, or the start of the drain.</summary>
    public required DateTimeOffset LastIncreaseAt { get; init; }

    public DateTimeOffset? LastDecreaseAt { get; init; }

    public DateTimeOffset? HeldUntil { get; init; }

    /// <summary>Green decisions in a row.</summary>
    public int GreenStreak { get; init; }

    /// <summary>
    /// Whether the operator's manual target drives this drain (<c>ManualDecision</c>), the
    /// brake's hold included.
    /// </summary>
    public bool IsManual { get; init; }

    /// <summary>Manual decisions in a row on a hot host or without the visitors' load.</summary>
    public int UnsafeStreak { get; init; }

    /// <summary>A drain from <paramref name="at"/>, at <paramref name="floor"/>.</summary>
    public static PaceMemory Start(DateTimeOffset at, int floor) => new()
    {
        State = PaceState.Starting,
        Allowance = floor,
        Reason = PaceReason.WarmingUp,
        Since = at,
        StartedAt = at,
        LastIncreaseAt = at,
    };

    /// <summary>A drain at the fixed <paramref name="pace"/>, which no decision moves.</summary>
    public static PaceMemory Fixed(DateTimeOffset at, int pace) =>
        Start(at, pace) with { State = PaceState.Fixed, Reason = PaceReason.Fixed };

    /// <summary>A drain at the operator's <paramref name="target"/> from the admin.</summary>
    public static PaceMemory Manual(DateTimeOffset at, int target) =>
        Start(at, target) with
        {
            State = PaceState.Manual,
            Reason = PaceReason.Manual,
            IsManual = true,
        };

    /// <summary>
    /// In <paramref name="state"/> for <paramref name="reason"/>: since <paramref name="at"/>,
    /// unless it was in that state already.
    /// </summary>
    public PaceMemory To(PaceState state, PaceReason reason, DateTimeOffset at) =>
        this with { State = state, Reason = reason, Since = state == State ? Since : at };
}
