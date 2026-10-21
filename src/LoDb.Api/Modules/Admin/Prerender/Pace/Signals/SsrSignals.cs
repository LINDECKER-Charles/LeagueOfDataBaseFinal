using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

/// <summary>
/// What the regulator reads of an SSR: its event loop's delay, its cgroup's pressure, and its
/// current lane since the read before (null when a counter went back).
/// </summary>
internal sealed record SsrSignals(
    double? EventLoopDelayP99Seconds,
    double? Pressure,
    LaneDelta? Current)
{
    /// <summary>The signals of <paramref name="now"/>, against <paramref name="before"/>.</summary>
    public static SsrSignals Of(SsrLoad? before, SsrLoad now)
    {
        ArgumentNullException.ThrowIfNull(now);
        return new SsrSignals(
            now.EventLoopDelayP99Seconds,
            now.CpuPressureSomeAvg10,
            LaneDelta.Between(before?.Lanes.Current, now.Lanes.Current));
    }
}
