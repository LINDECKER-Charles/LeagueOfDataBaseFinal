using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// The pace in force: the fixed pace unless a pass drains here, the floor at each drain and
/// once no decision renewed it, a line of log per change of state and a count per move.
/// </summary>
public sealed class PaceRegulatorTests
{
    private const long PassId = 7;

    [Fact]
    public void FixedPaceAppliesWhileNoPassDrainsHere()
    {
        using var rig = new PaceRig();

        Assert.False(rig.Regulator.IsDriven);
        Assert.Equal(PaceRig.FixedPace, rig.Regulator.Allowance());
        Assert.Null(rig.Regulator.Snapshot());
    }

    [Fact]
    public void EachDrainStartsAtTheFloorAndEndsOnTheFixedPace()
    {
        using var rig = new PaceRig();
        rig.Regulator.Begin(PassId);
        rig.DecideAfter(30, SignalLevel.Green);
        rig.DecideAfter(30, SignalLevel.Green);
        var climbed = rig.Regulator.Allowance();

        rig.Regulator.End();
        var ended = rig.Regulator.Allowance();
        rig.Regulator.Begin(PassId);

        Assert.Equal((60, PaceRig.FixedPace, PaceRig.Floor),
            (climbed, ended, rig.Regulator.Allowance()));
        var snapshot = rig.Regulator.Snapshot()!;
        Assert.Equal((PassId, PaceState.Starting, PaceReason.WarmingUp),
            (snapshot.PassId, snapshot.State, snapshot.Reason));
    }

    [Fact]
    public void AllowanceNoDecisionRenewedFallsBackToTheFloor()
    {
        using var rig = new PaceRig();
        rig.Regulator.Begin(PassId);
        rig.DecideAfter(30, SignalLevel.Green);
        rig.DecideAfter(30, SignalLevel.Green);

        rig.Clock.Advance(TimeSpan.FromSeconds(90));
        var renewed = rig.Regulator.Allowance();
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        var stale = rig.Regulator.Allowance();
        rig.DecideAfter(0, SignalLevel.Red);
        rig.Clock.Advance(TimeSpan.FromSeconds(91));

        Assert.Equal((60, PaceRig.Floor), (renewed, stale));
        Assert.Equal(PaceRig.Floor, rig.Regulator.Allowance());
    }

    [Fact]
    public void FixedModeKeepsTheFixedPaceAndSaysSoOncePerPass()
    {
        using var rig = new PaceRig(PaceMode.Fixed);

        rig.Regulator.Begin(PassId);
        rig.DecideAfter(30, SignalLevel.Red);
        rig.Regulator.Begin(PassId);
        rig.Regulator.Begin(PassId + 1);

        Assert.Equal(PaceRig.FixedPace, rig.Regulator.Allowance());
        var snapshot = rig.Regulator.Snapshot()!;
        Assert.Equal((PaceMode.Fixed, PaceState.Fixed, PaceReason.Fixed),
            (snapshot.Mode, snapshot.State, snapshot.Reason));
        Assert.Equal(["prerender.pace.fixed", "prerender.pace.fixed"], rig.EventNames);
    }

    [Fact]
    public void EachChangeOfStateIsOneLineAndEachMoveOneCount()
    {
        using var rig = new PaceRig();
        using var changes = new MetricCollector<long>(
            rig.Meters,
            PrerenderMetrics.MeterName,
            "lodb.prerender.pace.changes");
        rig.Regulator.Begin(PassId);

        Run(rig);

        Assert.Equal(
            [
                "prerender.pace.slowed", "prerender.pace.held", "prerender.pace.recovered",
                "prerender.pace.ceiling_reached", "prerender.pace.slowed",
                "prerender.pace.recovered", "prerender.pace.ceiling_reached",
            ],
            rig.EventNames);
        var counted = changes.GetMeasurementSnapshot()
            .GroupBy(static change => (string)change.Tags["change"]!)
            .ToDictionary(static group => group.Key, static group => group.Sum(m => m.Value));
        Assert.Equal((12, 2, 1, 1),
            (counted["increased"], counted["decreased"], counted["held"], counted["released"]));
    }

    [Fact]
    public void GaugesReportThePaceOnlyWhileAPassDrainsHere()
    {
        using var rig = new PaceRig();
        using var pace = new MetricCollector<int>(
            rig.Meters,
            PrerenderMetrics.MeterName,
            "lodb.prerender.pace.renders_per_minute");
        using var limit = new MetricCollector<int>(
            rig.Meters,
            PrerenderMetrics.MeterName,
            "lodb.prerender.pace.limit");

        pace.RecordObservableInstruments();
        var idle = pace.GetMeasurementSnapshot(clear: true).Count;
        rig.Regulator.Begin(PassId);
        rig.DecideAfter(30, SignalLevel.Amber);
        pace.RecordObservableInstruments();
        limit.RecordObservableInstruments();

        Assert.Equal(0, idle);
        Assert.Equal(PaceRig.Floor, Assert.Single(pace.GetMeasurementSnapshot()).Value);
        var limits = limit.GetMeasurementSnapshot();
        Assert.Equal(Enum.GetValues<PaceReason>().Length, limits.Count);
        var current = Assert.Single(limits, static measure => measure.Value == 1);
        Assert.Equal("host_cpu", current.Tags["reason"]);
    }

    // Slowed twice, held then released, climbed to the ceiling, slowed, back to the ceiling,
    // a hold band at the ceiling then green again: the ceiling is said once per climb.
    private static void Run(PaceRig rig)
    {
        rig.DecideAfter(30, SignalLevel.Green);
        rig.DecideAfter(30, SignalLevel.Green);
        rig.DecideAfter(30, SignalLevel.Amber);
        rig.DecideAfter(30, SignalLevel.Amber);
        rig.DecideAfter(30, SignalLevel.Red);
        rig.DecideAfter(30, SignalLevel.Red);
        rig.DecideAfter(300, SignalLevel.Green);
        Climb(rig, 20);
        rig.DecideAfter(30, SignalLevel.Amber);
        Climb(rig, 20);
        rig.DecideAfter(30, SignalLevel.Hold);
        Climb(rig, 2);
    }

    private static void Climb(PaceRig rig, int decisions)
    {
        for (var decision = 0; decision < decisions; decision++)
        {
            rig.DecideAfter(30, SignalLevel.Green);
        }
    }
}
