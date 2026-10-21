using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Drain;

/// <summary>
/// One try of a page of the queue: it ended the page, or failed in a way that may not last, or
/// the SSR refused it (wait, then again), or the pace held it back (again at once).
/// </summary>
internal sealed record DemandTry
{
    /// <summary>The pace holds the renders: the pacer already waited, nothing is counted.</summary>
    public static DemandTry Held { get; } = new() { IsHeld = true };

    /// <summary>How the page ended, if this try ended it.</summary>
    public DemandEnd? End { get; init; }

    /// <summary>Why the try failed, for a failure that may not last.</summary>
    public string? Failure { get; init; }

    /// <summary>The wait a refusal asks for.</summary>
    public TimeSpan Wait { get; init; }

    public bool IsHeld { get; init; }

    public static DemandTry Ended(DemandEnd end) => new() { End = end };

    public static DemandTry Failed(string reason) => new() { Failure = reason };

    public static DemandTry Refused(TimeSpan wait) => new() { Wait = wait };
}
