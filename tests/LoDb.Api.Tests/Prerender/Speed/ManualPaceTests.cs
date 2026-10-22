using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Manual;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

using LoDb.Api.Tests.Prerender.Pace;

namespace LoDb.Api.Tests.Prerender.Speed;

/// <summary>
/// The manual mode: the operator's target whatever the load, a new target at once, a change of
/// mode at the next decision; the emergency brake at once on a refused visitor of the latest
/// patch, after 3 minutes on a host busy at 90 % or without the visitors' load; the floor once
/// no decision renews the pace; the kill switch above all.
/// </summary>
public sealed class ManualPaceTests
{
    private const long PassId = 3;
    private const int Target = 600;
    private const int Calm = 0;
    private const double Quiet = 0.4;
    private const double Hot = 0.95;

    [Fact]
    public void ManualDrainRunsAtTheTargetWhateverTheLoadAndANewTargetAppliesAtOnce()
    {
        using var rig = new PaceRig();
        rig.Manual(Target);
        rig.Regulator.Begin(PassId);
        var started = rig.Regulator.Allowance();
        rig.DecideManual(30, hostBusy: 0.85, visitorsRefused: Calm);

        rig.Manual(450);

        Assert.Equal((Target, 450), (started, rig.Regulator.Allowance()));
        var snapshot = rig.Regulator.Snapshot()!;
        Assert.Equal(
            (true, PaceState.Manual, PaceReason.Manual),
            (snapshot.IsManual, snapshot.State, snapshot.Reason));
    }

    [Fact]
    public void TargetOverTheHardMaximumIsHeldToIt()
    {
        using var rig = new PaceRig();
        rig.Manual(PaceOptions.DefaultManualMaxRendersPerMinute + 400);
        rig.Regulator.Begin(PassId);

        Assert.Equal(PaceOptions.DefaultManualMaxRendersPerMinute, rig.Regulator.Allowance());
    }

    [Fact]
    public void RefusedVisitorBrakesAtOnceForFiveMinutesThenTheTargetResumes()
    {
        using var rig = new PaceRig();
        rig.Manual(Target);
        rig.Regulator.Begin(PassId);

        rig.DecideManual(30, Quiet, visitorsRefused: 2);
        var braked = rig.Regulator.Snapshot()!;
        rig.DecideManual(270, Quiet, Calm);
        var stillHeld = rig.Regulator.Allowance();
        rig.DecideManual(30, Quiet, Calm);

        Assert.Equal(
            (PaceState.Held, PaceReason.VisitorsRefused, 0),
            (braked.State, braked.Reason, braked.RendersPerMinute));
        Assert.Equal(rig.Clock.GetUtcNow(), braked.HeldUntil);
        Assert.Equal((0, Target), (stillHeld, rig.Regulator.Allowance()));
        Assert.Equal(["prerender.pace.held", "prerender.pace.recovered"], Pace(rig));
    }

    [Fact]
    public void HotHostBrakesAfterThreeMinutesOnly()
    {
        using var rig = new PaceRig();
        rig.Manual(Target);
        rig.Regulator.Begin(PassId);
        for (var decision = 1; decision < ManualDecision.UnsafeDecisionsToBrake; decision++)
        {
            rig.DecideManual(30, Hot, Calm);
        }

        var beforeBrake = rig.Regulator.Allowance();
        rig.DecideManual(30, Hot, Calm);

        Assert.Equal((Target, 0), (beforeBrake, rig.Regulator.Allowance()));
        Assert.Equal(PaceReason.HostCpu, rig.Regulator.Snapshot()!.Reason);
    }

    [Fact]
    public void VisitorsSlowedOrApiThrottledBrakesAfterThreeMinutesUnderTheHostThreshold()
    {
        using var rig = new PaceRig();
        rig.Manual(Target);
        rig.Regulator.Begin(PassId);
        var throttled = new SignalReading(SignalLevel.Red, PaceReason.ApiCpu, 0.25);
        for (var decision = 1; decision < ManualDecision.UnsafeDecisionsToBrake; decision++)
        {
            rig.DecideManual(30, Quiet, Calm, throttled);
        }

        var beforeBrake = rig.Regulator.Allowance();
        rig.DecideManual(30, Quiet, Calm, throttled);

        Assert.Equal((Target, 0), (beforeBrake, rig.Regulator.Allowance()));
        Assert.Equal(PaceReason.ApiCpu, rig.Regulator.Snapshot()!.Reason);
    }

    [Fact]
    public void CoolerDecisionStartsTheHotStreakAgain()
    {
        using var rig = new PaceRig();
        rig.Manual(Target);
        rig.Regulator.Begin(PassId);
        for (var decision = 0; decision < 10; decision++)
        {
            rig.DecideManual(30, decision % 5 == 4 ? Quiet : Hot, Calm);
        }

        Assert.Equal(Target, rig.Regulator.Allowance());
    }

    [Fact]
    public void VisitorsLoadUnreadForThreeMinutesBrakes()
    {
        using var rig = new PaceRig();
        rig.Manual(Target);
        rig.Regulator.Begin(PassId);

        for (var decision = 0; decision < ManualDecision.UnsafeDecisionsToBrake; decision++)
        {
            rig.DecideManual(30, Quiet, visitorsRefused: null);
        }

        var snapshot = rig.Regulator.Snapshot()!;
        Assert.Equal((PaceState.Held, PaceReason.SignalMissing), (snapshot.State, snapshot.Reason));
    }

    [Fact]
    public void ModeChangeAppliesAtTheNextDecisionBackToTheFloorWhenAdaptive()
    {
        using var rig = new PaceRig();
        rig.Regulator.Begin(PassId);
        rig.Manual(Target);
        var beforeDecision = rig.Regulator.Allowance();
        rig.DecideManual(30, Quiet, Calm);
        var manual = rig.Regulator.Allowance();

        rig.Settings.Apply(new PaceSetting(PaceChoice.Adaptive, Target)
        {
            UpdatedAt = rig.Clock.GetUtcNow(),
        });
        var pending = rig.Regulator.Allowance();
        rig.DecideAfter(30, SignalLevel.Green);

        Assert.Equal((PaceRig.Floor, Target), (beforeDecision, manual));
        Assert.Equal(PaceRig.Floor, pending);
        var snapshot = rig.Regulator.Snapshot()!;
        Assert.Equal((false, PaceState.Starting), (snapshot.IsManual, snapshot.State));
        Assert.Contains("prerender.pace.setting_applied", rig.EventNames);
    }

    [Fact]
    public void ManualPaceNoDecisionRenewedFallsBackToTheFloor()
    {
        using var rig = new PaceRig();
        rig.Manual(Target);
        rig.Regulator.Begin(PassId);

        rig.Clock.Advance(PaceTiming.Stale + TimeSpan.FromSeconds(1));

        Assert.Equal(PaceRig.Floor, rig.Regulator.Allowance());
    }

    [Fact]
    public void KillSwitchKeepsTheFixedPaceWhateverTheSetting()
    {
        using var rig = new PaceRig(PaceMode.Fixed);
        rig.Manual(Target);
        rig.Regulator.Begin(PassId);
        rig.DecideManual(30, Hot, visitorsRefused: 3);

        Assert.Equal(PaceRig.FixedPace, rig.Regulator.Allowance());
        Assert.False(rig.Regulator.Snapshot()!.IsManual);
    }

    private static IEnumerable<string> Pace(PaceRig rig) =>
        rig.EventNames.Where(static name =>
            name is "prerender.pace.held" or "prerender.pace.recovered");
}
