using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

/// <summary>
/// The rating of each signal, pure: a value against its band, too few renders as green, a
/// source that cannot be read as missing (amber).
/// </summary>
internal static class PaceSignals
{
    /// <summary><paramref name="value"/> against <paramref name="band"/>.</summary>
    public static SignalReading Rate(double? value, SignalBand band, PaceReason reason)
    {
        ArgumentNullException.ThrowIfNull(band);
        return value is { } read
            ? new SignalReading(band.LevelOf(read), reason, read)
            : SignalReading.Missing;
    }

    /// <summary>V3: the visitors' current-lane mean render.</summary>
    public static SignalReading VisitorsRender(LaneDelta? current)
    {
        if (current is null)
        {
            return SignalReading.Missing;
        }

        return current.Renders < PaceThresholds.VisitorsRendersNeeded
            ? SignalReading.Green
            : Rate(
                current.RenderSeconds / current.Renders,
                PaceThresholds.VisitorsRender,
                PaceReason.VisitorsSsr);
    }

    /// <summary>V4: a visitor refused for the server's or the lane's sake is red.</summary>
    public static SignalReading VisitorsRefused(LaneDelta? current) => current switch
    {
        null => SignalReading.Missing,
        { Refused: > 0 } => new SignalReading(
            SignalLevel.Red,
            PaceReason.VisitorsRefused,
            current.Refused),
        _ => SignalReading.Green,
    };

    /// <summary>P2: the pass's median render against its baseline.</summary>
    public static SignalReading PassLatency(PassLatencySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (sample.Renders < PaceThresholds.PassRendersNeeded
            || sample.MedianSeconds is not { } median)
        {
            return SignalReading.Green;
        }

        var baseline = Math.Max(
            PaceThresholds.PassBaselineFloorSeconds,
            sample.BaselineSeconds ?? median);
        var level = LatencyLevelOf(median, median / baseline);
        return new SignalReading(level, PaceReason.PassLatency, median);
    }

    private static SignalLevel LatencyLevelOf(double median, double ratio) =>
        median >= PaceThresholds.PassLatencyRedSeconds ? SignalLevel.Red
        : ratio >= PaceThresholds.PassLatencyAmberRatio
            && median >= PaceThresholds.PassLatencyAmberSeconds ? SignalLevel.Amber
        : ratio >= PaceThresholds.PassLatencyHoldRatio ? SignalLevel.Hold
        : SignalLevel.Green;
}
