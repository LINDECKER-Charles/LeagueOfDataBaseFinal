using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// The regulator's rule, decision by decision every 30 s: up a step a minute while green and
/// used, frozen in a hold band, halved on amber, held on red, back at the floor after.
/// </summary>
public sealed class PaceDecisionTests
{
    private const int Evaluation = 30;
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly PaceOptions Options = new();

    [Fact]
    public void DrainStartsAtTheFloorAndWaitsOutItsFirstMinute()
    {
        var memory = PaceMemory.Start(Start, PaceRig.Floor);

        var warming = Next(memory, 30, SignalLevel.Green);
        var after = Next(warming, 60, SignalLevel.Green);

        Assert.Equal((PaceState.Starting, 30, PaceReason.WarmingUp),
            (warming.State, warming.Allowance, warming.Reason));
        Assert.Equal((PaceState.Climbing, 60, PaceReason.None),
            (after.State, after.Allowance, after.Reason));
    }

    [Fact]
    public void GreenClimbsOneStepAMinuteToTheCeilingInSevenMinutes()
    {
        var memory = PaceMemory.Start(Start, PaceRig.Floor);
        var allowances = new List<int>();

        for (var second = Evaluation; second <= 450; second += Evaluation)
        {
            memory = Next(memory, second, SignalLevel.Green);
            allowances.Add(memory.Allowance);
        }

        Assert.Equal(
            [30, 60, 60, 90, 90, 120, 120, 150, 150, 180, 180, 210, 210, 240, 240],
            allowances);
        Assert.Equal((PaceState.Steady, PaceReason.Ceiling), (memory.State, memory.Reason));
    }

    [Fact]
    public void IncreaseNeedsTwoGreenDecisionsInARow()
    {
        var memory = Next(PaceMemory.Start(Start, PaceRig.Floor), 30, SignalLevel.Green);
        memory = Next(memory, 60, SignalLevel.Hold);

        var once = Next(memory, 90, SignalLevel.Green);
        var twice = Next(once, 120, SignalLevel.Green);

        Assert.Equal((PaceState.Steady, PaceReason.HostCpu), (memory.State, memory.Reason));
        Assert.Equal((30, 60), (once.Allowance, twice.Allowance));
    }

    [Fact]
    public void HoldBandFreezesThePace()
    {
        var memory = Climbed(150);

        var held = Next(memory, 600, SignalLevel.Hold);

        Assert.Equal((PaceState.Steady, 150, PaceReason.HostCpu),
            (held.State, held.Allowance, held.Reason));
    }

    [Fact]
    public void AmberHalvesThePaceDownToTheFloorAtMost()
    {
        var memory = Climbed(PaceRig.Ceiling);
        var allowances = new List<int>();

        for (var second = 600; second <= 720; second += Evaluation)
        {
            memory = Next(memory, second, SignalLevel.Amber);
            allowances.Add(memory.Allowance);
        }

        Assert.Equal([120, 60, 30, 30, 30], allowances);
        Assert.Equal((PaceState.Slowed, PaceReason.HostCpu), (memory.State, memory.Reason));
    }

    [Fact]
    public void NoIncreaseForThreeMinutesAfterADecrease()
    {
        var memory = Next(Climbed(120), 600, SignalLevel.Amber);
        var allowances = new List<int>();

        for (var second = 630; second <= 810; second += Evaluation)
        {
            memory = Next(memory, second, SignalLevel.Green);
            allowances.Add(memory.Allowance);
        }

        Assert.Equal([60, 60, 60, 60, 60, 90, 90], allowances);
    }

    [Fact]
    public void RedHoldsFiveMinutesFromItsLastDecisionThenRestartsAtTheFloor()
    {
        var memory = Next(Climbed(PaceRig.Ceiling), 600, SignalLevel.Red);
        var extended = Next(memory, 660, SignalLevel.Red);
        var stillHeld = Next(extended, 930, SignalLevel.Green);
        var released = Next(stillHeld, 960, SignalLevel.Green);

        Assert.Equal((PaceState.Held, 0), (memory.State, memory.Allowance));
        Assert.Equal(Start.AddSeconds(660 + 300), extended.HeldUntil);
        Assert.Equal((PaceState.Held, 0), (stillHeld.State, stillHeld.Allowance));
        Assert.Equal((PaceState.Starting, PaceRig.Floor, null),
            (released.State, released.Allowance, released.HeldUntil));
        var cooling = Next(Next(released, 990, SignalLevel.Green), 1020, SignalLevel.Green);
        Assert.Equal(PaceRig.Floor, cooling.Allowance);
    }

    [Fact]
    public void PassThatUsesLessThanItsAllowanceDoesNotClimb()
    {
        var memory = PaceMemory.Start(Start, PaceRig.Floor);

        for (var second = Evaluation; second <= 300; second += Evaluation)
        {
            memory = PaceDecision.Next(memory, At(second, SignalLevel.Green, 23), Options);
        }

        Assert.Equal((PaceState.Starting, PaceRig.Floor), (memory.State, memory.Allowance));
        var used = PaceDecision.Next(memory, At(330, SignalLevel.Green, 24), Options);
        Assert.Equal(60, used.Allowance);
    }

    private static PaceMemory Climbed(int allowance) =>
        PaceMemory.Start(Start, PaceRig.Floor) with
        {
            State = PaceState.Climbing,
            Allowance = allowance,
            Reason = PaceReason.None,
        };

    private static PaceMemory Next(PaceMemory memory, int second, SignalLevel level) =>
        PaceDecision.Next(memory, At(second, level, memory.Allowance), Options);

    private static PaceEvaluation At(int second, SignalLevel level, int achieved) =>
        PaceRig.Evaluation(level, Start.AddSeconds(second), achieved);
}
