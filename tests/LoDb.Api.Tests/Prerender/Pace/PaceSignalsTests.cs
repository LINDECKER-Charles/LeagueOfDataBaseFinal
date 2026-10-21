using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;
using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// Each signal at the bounds of its bands; too few renders say nothing; a source that cannot
/// be read, or an SSR that restarted, is missing; the worst signal wins.
/// </summary>
public sealed class PaceSignalsTests
{
    public static TheoryData<string, double, string> Bounds => new()
    {
        { "HostBusy", 0.499, "Green" },
        { "HostBusy", 0.50, "Hold" },
        { "HostBusy", 0.55, "Amber" },
        { "HostBusy", 0.65, "Red" },
        { "HostSteal", 0.249, "Green" },
        { "HostSteal", 0.25, "Amber" },
        { "HostSteal", 0.40, "Red" },
        { "HostUsed", 0.399, "Green" },
        { "HostUsed", 0.40, "Hold" },
        { "HostUsed", 0.45, "Amber" },
        { "HostUsed", 0.55, "Red" },
        { "VisitorsEventLoop", 0.049, "Green" },
        { "VisitorsEventLoop", 0.050, "Hold" },
        { "VisitorsEventLoop", 0.080, "Amber" },
        { "VisitorsEventLoop", 0.250, "Red" },
        { "VisitorsPressure", 0.019, "Green" },
        { "VisitorsPressure", 0.02, "Hold" },
        { "VisitorsPressure", 0.05, "Amber" },
        { "VisitorsPressure", 0.15, "Red" },
        { "VisitorsRender", 0.299, "Green" },
        { "VisitorsRender", 0.3, "Hold" },
        { "VisitorsRender", 0.5, "Amber" },
        { "VisitorsRender", 9.0, "Amber" },
        { "ApiThrottled", 0.019, "Green" },
        { "ApiThrottled", 0.02, "Hold" },
        { "ApiThrottled", 0.05, "Amber" },
        { "ApiThrottled", 0.20, "Red" },
        { "PassPressure", 0.199, "Green" },
        { "PassPressure", 0.20, "Hold" },
        { "PassPressure", 0.30, "Amber" },
        { "PassPressure", 1.0, "Amber" },
    };

    public static TheoryData<int, double?, double?, string> Latencies => new()
    {
        { 9, 3.0, 0.1, "Green" },
        { 10, null, 0.1, "Green" },
        { 10, 0.18, 0.125, "Green" },
        { 10, 0.1875, 0.125, "Hold" },
        { 10, 0.125, 0.0625, "Hold" },
        { 10, 0.25, 0.125, "Amber" },
        { 10, 1.5, 1.4, "Red" },
        { 10, 0.8, null, "Green" },
        { 10, 0.1, 0.01, "Hold" },
    };

    [Theory]
    [MemberData(nameof(Bounds))]
    public void SignalTakesTheLevelOfItsBand(string band, double value, string level)
    {
        var bands = typeof(PaceThresholds).GetField(band)!.GetValue(null) as SignalBand;

        var reading = PaceSignals.Rate(value, bands!, PaceReason.HostCpu);

        Assert.Equal(
            (Enum.Parse<SignalLevel>(level), PaceReason.HostCpu, (double?)value),
            (reading.Level, reading.Reason, reading.Value));
    }

    [Fact]
    public void UnreadableValueIsMissingAndSlowsThePass()
    {
        var reading = PaceSignals.Rate(null, PaceThresholds.HostBusy, PaceReason.HostCpu);

        Assert.Equal((SignalLevel.Amber, PaceReason.SignalMissing),
            (reading.Level, reading.Reason));
    }

    [Theory]
    [InlineData(4, 40.0, 0, "Green")]
    [InlineData(5, 2.5, 0, "Amber")]
    [InlineData(0, 0.0, 1, "Red")]
    public void VisitorsCurrentLaneNeedsFiveRendersAndRefusesNothing(
        long renders,
        double seconds,
        long refused,
        string level)
    {
        var current = new LaneDelta(renders, seconds, refused);

        var worst = new[]
        {
            PaceSignals.VisitorsRender(current),
            PaceSignals.VisitorsRefused(current),
        }.MaxBy(static reading => reading.Level)!;

        Assert.Equal(Enum.Parse<SignalLevel>(level), worst.Level);
    }

    [Theory]
    [MemberData(nameof(Latencies))]
    public void PassMedianIsHeldAgainstItsBaseline(
        int renders,
        double? median,
        double? baseline,
        string level)
    {
        var reading = PaceSignals.PassLatency(new PassLatencySample(renders, median, baseline));

        Assert.Equal(Enum.Parse<SignalLevel>(level), reading.Level);
    }

    [Fact]
    public void SsrThatRestartedReadsAsMissing()
    {
        var before = Load(renders: 100, refused: 2);

        var restarted = SsrSignals.Of(before, Load(renders: 3, refused: 0));
        var first = SsrSignals.Of(null, Load(renders: 100, refused: 2));
        var later = SsrSignals.Of(before, Load(renders: 110, refused: 2));

        Assert.Null(restarted.Current);
        Assert.Equal(SignalLevel.Amber, PaceSignals.VisitorsRender(restarted.Current).Level);
        Assert.Equal(
            PaceReason.SignalMissing,
            PaceSignals.VisitorsRefused(restarted.Current).Reason);
        Assert.Equal(new LaneDelta(0, 0, 0), first.Current);
        Assert.Equal(new LaneDelta(10, 5.0, 0), later.Current);
    }

    [Fact]
    public void WorstSignalWinsAndTheFirstAmongEquals()
    {
        var calm = Readings(new HostShares(0.2, 0.05, 0.2));
        var busy = calm with { Host = new HostShares(0.56, 0.05, 0.2), ApiThrottled = 0.06 };
        var red = calm with { Host = new HostShares(0.2, 0.41, 0.2) };
        var blind = calm with { Visitors = null };

        Assert.Equal(SignalLevel.Green, PaceBoard.Of(calm).Worst.Level);
        var worst = PaceBoard.Of(busy).Worst;
        Assert.Equal(
            (SignalLevel.Amber, PaceReason.HostCpu, (double?)0.56),
            (worst.Level, worst.Reason, worst.Value));
        Assert.Equal(PaceReason.HostSteal, PaceBoard.Of(red).Worst.Reason);
        Assert.Equal(PaceReason.SignalMissing, PaceBoard.Of(blind).Worst.Reason);
        Assert.Equal(0.56, PaceBoard.Of(busy).HostBusy);
    }

    private static PaceReadings Readings(HostShares host) => new()
    {
        Host = host,
        ApiThrottled = 0,
        Visitors = new SsrSignals(0.01, 0.001, new LaneDelta(2, 0.2, 0)),
        Pass = new SsrSignals(0.01, 0.05, new LaneDelta(0, 0, 0)),
        PassLatency = new PassLatencySample(0, null, null),
    };

    private static SsrLoad Load(long renders, long refused) => new()
    {
        CpuSeconds = 1,
        Lanes = new SsrLanes
        {
            Current = new SsrLaneLoad
            {
                Renders = renders,
                RenderSeconds = renders * 0.5,
                Refused = refused,
            },
            Pinned = new SsrLaneLoad { Renders = 0, RenderSeconds = 0, Refused = 0 },
            Prerender = new SsrLaneLoad { Renders = 0, RenderSeconds = 0, Refused = 0 },
        },
    };
}
