namespace LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

/// <summary>
/// The signals' bands (docs/produit/pre-rendu-catalogue-cdn.md, lot 2, « Ce qu'il lit »), the
/// same on every host. Shares are 0..1, times in seconds.
/// </summary>
/// <remarks>
/// The VPS is under 50 % busy 96 % of the time: the pass only climbs in that hollow. 45 % used
/// over 30 minutes stays 15 points under the hoster's throttling alert; 25 % of steal is its
/// p95 over 7 days, 40 % the plateau of a throttling. The production event loop's p99 has a
/// p95 of 40 to 50 ms; the visitors' SSR pressure is 0.1 % at rest, 0.2 to 3 % beside the
/// dedicated pass. 20 % of throttled API time is <c>ConteneurCpuBride</c>'s threshold.
/// </remarks>
internal static class PaceThresholds
{
    /// <summary>H1: the host busy, steal included, over a minute.</summary>
    public static readonly SignalBand HostBusy = new(0.50, 0.55, 0.65);

    /// <summary>H2: the host's stolen share over a minute: no hold band.</summary>
    public static readonly SignalBand HostSteal = new(0.25, 0.25, 0.40);

    /// <summary>H3: the host used, steal excluded, over 30 minutes.</summary>
    public static readonly SignalBand HostUsed = new(0.40, 0.45, 0.55);

    /// <summary>V1: the visitors' SSR event loop delay p99, in seconds.</summary>
    public static readonly SignalBand VisitorsEventLoop = new(0.050, 0.080, 0.250);

    /// <summary>V2: the visitors' SSR cgroup pressure, some avg10.</summary>
    public static readonly SignalBand VisitorsPressure = new(0.02, 0.05, 0.15);

    /// <summary>V3: the visitors' current-lane mean render, in seconds: never red.</summary>
    public static readonly SignalBand VisitorsRender = new(0.3, 0.5, null);

    /// <summary>A1: the API's throttled share of the wall time over a minute.</summary>
    public static readonly SignalBand ApiThrottled = new(0.02, 0.05, 0.20);

    /// <summary>P1: the pass's SSR cgroup pressure, some avg10: never red.</summary>
    public static readonly SignalBand PassPressure = new(0.20, 0.30, null);

    /// <summary>P2: the pass's median over its baseline, from which it holds.</summary>
    public const double PassLatencyHoldRatio = 1.5;

    /// <summary>
    /// P2: the ratio from which it is amber, with <see cref="PassLatencyAmberSeconds"/>.
    /// </summary>
    public const double PassLatencyAmberRatio = 2.0;

    /// <summary>
    /// P2: an amber median is this slow at least: a fast render that doubles is noise.
    /// </summary>
    public const double PassLatencyAmberSeconds = 0.25;

    /// <summary>P2: a median this slow is red, whatever the baseline.</summary>
    public const double PassLatencyRedSeconds = 1.5;

    /// <summary>P2: the baseline is never under this: a page is not rendered faster.</summary>
    public const double PassBaselineFloorSeconds = 0.05;

    /// <summary>V3: renders the window needs to say anything.</summary>
    public const int VisitorsRendersNeeded = 5;

    /// <summary>P2: renders the minute needs to say anything.</summary>
    public const int PassRendersNeeded = 10;
}
