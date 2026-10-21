using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

/// <summary>
/// The signals of one evaluation: the worst wins, and its reason is the one the admin and the
/// logs name. Among equals, the first in the order of the spec's table (host, visitors, API,
/// pass).
/// </summary>
/// <param name="Worst">The gravest reading.</param>
/// <param name="HostBusy">The host's busy share over the last minute, for the admin.</param>
/// <param name="VisitorsRefused">
/// Visitors the latest-patch lane refused since the read before; null while the visitors' load
/// cannot be read. The manual mode's brake reads it.
/// </param>
/// <param name="VisitorsHurt">
/// The first red signal of what the visitors wait on (their SSR's event loop or CPU pressure,
/// the API's throttling); null when none is red. The manual mode's brake reads it.
/// </param>
internal sealed record PaceBoard(
    SignalReading Worst,
    double? HostBusy,
    long? VisitorsRefused = null,
    SignalReading? VisitorsHurt = null)
{
    /// <summary>Every signal of <paramref name="readings"/>, rated.</summary>
    public static PaceBoard Of(PaceReadings readings)
    {
        ArgumentNullException.ThrowIfNull(readings);
        var worst = ReadingsOf(readings)
            .Aggregate(static (worst, next) => next.Level > worst.Level ? next : worst);
        return new PaceBoard(
            worst,
            readings.Host.Busy,
            RefusedOf(readings.Visitors),
            HurtOf(readings));
    }

    // A counter that went back (the SSR restarted) refused nothing anyone can tell.
    private static long? RefusedOf(SsrSignals? visitors) =>
        visitors is null ? null : visitors.Current?.Refused ?? 0;

    // Rated apart from the worst: a red host reading, first in the spec's order, would hide
    // a visitors' red one, which the manual brake must still see.
    private static SignalReading? HurtOf(PaceReadings readings)
    {
        SignalReading[] hurts =
        [
            PaceSignals.Rate(
                readings.Visitors?.EventLoopDelayP99Seconds,
                PaceThresholds.VisitorsEventLoop,
                PaceReason.VisitorsSsr),
            PaceSignals.Rate(
                readings.Visitors?.Pressure,
                PaceThresholds.VisitorsPressure,
                PaceReason.VisitorsSsr),
            PaceSignals.Rate(readings.ApiThrottled, PaceThresholds.ApiThrottled, PaceReason.ApiCpu),
        ];
        return hurts.FirstOrDefault(static reading => reading.Level == SignalLevel.Red);
    }

    private static IEnumerable<SignalReading> ReadingsOf(PaceReadings readings)
    {
        var (host, visitors) = (readings.Host, readings.Visitors);
        yield return PaceSignals.Rate(host.Busy, PaceThresholds.HostBusy, PaceReason.HostCpu);
        yield return PaceSignals.Rate(host.Steal, PaceThresholds.HostSteal, PaceReason.HostSteal);
        yield return PaceSignals.Rate(
            host.Used,
            PaceThresholds.HostUsed,
            PaceReason.HostCpuSustained);
        yield return PaceSignals.Rate(
            visitors?.EventLoopDelayP99Seconds,
            PaceThresholds.VisitorsEventLoop,
            PaceReason.VisitorsSsr);
        yield return PaceSignals.Rate(
            visitors?.Pressure,
            PaceThresholds.VisitorsPressure,
            PaceReason.VisitorsSsr);
        yield return visitors is null
            ? SignalReading.Missing
            : PaceSignals.VisitorsRender(visitors.Current);
        yield return visitors is null
            ? SignalReading.Missing
            : PaceSignals.VisitorsRefused(visitors.Current);
        yield return PaceSignals.Rate(
            readings.ApiThrottled,
            PaceThresholds.ApiThrottled,
            PaceReason.ApiCpu);
        yield return PaceSignals.Rate(
            readings.Pass?.Pressure,
            PaceThresholds.PassPressure,
            PaceReason.PassSsrPressure);
        yield return PaceSignals.PassLatency(readings.PassLatency);
    }
}
