namespace LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;

/// <summary>
/// What an SSR's <c>GET /load</c> answers on its metrics port (src/LoDb.Web/src/server/load):
/// its CPU, its event loop's delay, its cgroup's pressure, and its renders per lane, all
/// cumulative but the delay and the pressure.
/// </summary>
internal sealed record SsrLoad
{
    public required double CpuSeconds { get; init; }

    /// <summary>The p99 over the SSR's last closed 30-s window; null before the first.</summary>
    public double? EventLoopDelayP99Seconds { get; init; }

    /// <summary>The cgroup's <c>cpu.pressure</c> some avg10, as a 0..1 share.</summary>
    public double? CpuPressureSomeAvg10 { get; init; }

    public double? CpuThrottledSeconds { get; init; }

    public required SsrLanes Lanes { get; init; }
}
