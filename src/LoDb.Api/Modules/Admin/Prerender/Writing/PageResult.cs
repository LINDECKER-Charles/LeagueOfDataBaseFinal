using LoDb.Api.Modules.Admin.Prerender.Telemetry;

namespace LoDb.Api.Modules.Admin.Prerender.Writing;

/// <summary>How the pass left one page, after its tries.</summary>
internal sealed record PageResult
{
    public static PageResult Skipped { get; } = new() { Outcome = PageOutcome.Skipped };

    /// <summary>The pass was paused or cancelled: the page waits where it is.</summary>
    public static PageResult Interrupted { get; } = new();

    /// <summary>
    /// What the page ended as: written, skipped or failed. Null when it did not end, the pass
    /// having stopped or being misconfigured: its cursor stays on it.
    /// </summary>
    public PageOutcome? Outcome { get; init; }

    /// <summary>Bytes written to the store, for a written page.</summary>
    public long Bytes { get; init; }

    /// <summary>Why the page failed, or why the pass cannot go on.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Whether the store or the SSR refuses the pass itself: a key rotated, a policy without
    /// the writer, a public host the SSR does not allow. Every page would fail alike.
    /// </summary>
    public bool IsMisconfigured { get; init; }

    /// <summary>The SSR's refusals waited out on the way.</summary>
    public int Refusals { get; init; }

    public static PageResult Written(long bytes) =>
        new() { Outcome = PageOutcome.Written, Bytes = bytes };

    public static PageResult Failed(string reason) =>
        new() { Outcome = PageOutcome.Failed, Reason = reason };

    public static PageResult Misconfigured(string reason) =>
        new() { Reason = reason, IsMisconfigured = true };
}
