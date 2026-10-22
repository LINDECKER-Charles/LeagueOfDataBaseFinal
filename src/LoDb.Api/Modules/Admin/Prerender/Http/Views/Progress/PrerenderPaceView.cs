namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;

/// <summary>
/// The pace of the pass as the regulator of the instance answering sets it, while that
/// instance drains the pass (docs/produit/pre-rendu-catalogue-cdn.md, lot 2).
/// </summary>
internal sealed record PrerenderPaceView
{
    /// <summary>
    /// <c>adaptive</c>, <c>manual</c> (the operator's target from the admin), or <c>fixed</c>
    /// when the regulation is off.
    /// </summary>
    public required string Mode { get; init; }

    /// <summary>
    /// <c>starting</c>, <c>climbing</c>, <c>steady</c>, <c>slowed</c>, <c>held</c>,
    /// <c>fixed</c> or <c>manual</c>.
    /// </summary>
    public required string State { get; init; }

    /// <summary>Renders the pass may start per minute now: 0 while held.</summary>
    public required int RendersPerMinute { get; init; }

    /// <summary>The floor (LoDb:Prerender:Pace:MinRendersPerMinute).</summary>
    public required int MinRendersPerMinute { get; init; }

    /// <summary>The ceiling (LoDb:Prerender:Pace:MaxRendersPerMinute).</summary>
    public required int MaxRendersPerMinute { get; init; }

    /// <summary>
    /// What limits the pace, the worst signal or the regulator's own rule: <c>warming-up</c>,
    /// <c>ceiling</c>, <c>host-cpu</c>, <c>host-cpu-sustained</c>, <c>host-steal</c>,
    /// <c>visitors-ssr</c>, <c>visitors-refused</c>, <c>api-cpu</c>, <c>pass-latency</c>,
    /// <c>pass-ssr-pressure</c>, <c>signal-missing</c>, <c>fixed</c> or <c>manual</c>; null while
    /// it climbs freely.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// The VPS's busy share, steal included, over the last minute (0..1, 3 decimals); null
    /// before the regulator's first decision, and in fixed mode.
    /// </summary>
    public decimal? HostCpu { get; init; }

    /// <summary>Until when the pass starts no render, at least; null unless held.</summary>
    public DateTimeOffset? HeldUntil { get; init; }

    /// <summary>Since when the regulator is in its state.</summary>
    public required DateTimeOffset Since { get; init; }
}
