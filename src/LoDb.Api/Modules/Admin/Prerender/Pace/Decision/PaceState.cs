namespace LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

/// <summary>Where the regulator stands.</summary>
internal enum PaceState
{
    /// <summary>No pass drains here: the fixed pace applies.</summary>
    Idle,

    /// <summary>At the floor, from the start of a drain or the end of a hold.</summary>
    Starting,

    /// <summary>Every signal green: one step up at a time.</summary>
    Climbing,

    /// <summary>A signal in its hold band, or the ceiling reached: the pace stays.</summary>
    Steady,

    /// <summary>Halved by an amber signal, the floor at most.</summary>
    Slowed,

    /// <summary>A red signal: no new render starts until the hold ends.</summary>
    Held,

    /// <summary>The regulation is off: the fixed pace, whatever the load.</summary>
    Fixed,
}
