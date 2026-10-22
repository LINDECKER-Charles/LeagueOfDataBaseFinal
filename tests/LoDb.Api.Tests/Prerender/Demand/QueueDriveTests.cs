using System.Globalization;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;
using LoDb.Api.Tests.Prerender.Pace;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// A queue drives the regulator while it has pages: alone, it renders at the regulator's or
/// the operator's pace, never at the fixed pace no signal watches; beside a pass or the other
/// queue, it shares the pace as it is, and the pace stops once none drives it.
/// </summary>
public sealed class QueueDriveTests(PostgresContainerFixture postgres)
{
    private const long PassId = 7;

    // The two automatic queues, as their drains name themselves.
    private const string Demand = "prerender:demand";
    private const string Revision = "prerender:revision";
    private const int ManualTarget = 300;
    private const double Tolerance = 0.05;
    private static readonly TimeSpan FloorInterval = TimeSpan.FromMinutes(1) / PaceRig.Floor;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void QueueAloneStartsAtTheFloorAndStopsWithIt()
    {
        using var rig = new PaceRig();

        rig.Regulator.BeginQueue(Demand);
        var driven = (rig.Regulator.IsDriven, rig.Regulator.Allowance());
        var snapshot = rig.Regulator.Snapshot()!;
        rig.Regulator.EndQueue(Demand);

        Assert.Equal((true, PaceRig.Floor), driven);
        Assert.Equal(0, snapshot.PassId);
        Assert.Equal((false, PaceRig.FixedPace),
            (rig.Regulator.IsDriven, rig.Regulator.Allowance()));
        Assert.Null(rig.Regulator.Snapshot());
    }

    [Fact]
    public void QueueAloneFollowsTheOperatorsTarget()
    {
        using var rig = new PaceRig();
        rig.Manual(ManualTarget);

        rig.Regulator.BeginQueue(Demand);

        Assert.Equal(ManualTarget, rig.Regulator.Allowance());
    }

    [Fact]
    public void QueueJoinsAPassAsItIsAndOutlivesIt()
    {
        using var rig = new PaceRig();
        rig.Regulator.Begin(PassId);
        rig.DecideAfter(30, SignalLevel.Green);
        rig.DecideAfter(30, SignalLevel.Green);

        rig.Regulator.BeginQueue(Demand);
        rig.Regulator.BeginQueue(Demand);
        var joined = rig.Regulator.Allowance();
        rig.Regulator.End();
        var afterPass = rig.Regulator.Allowance();
        var snapshot = rig.Regulator.Snapshot()!;
        rig.Regulator.EndQueue(Demand);

        Assert.Equal((60, 60), (joined, afterPass));
        Assert.Equal((0, PaceState.Climbing), (snapshot.PassId, snapshot.State));
        Assert.False(rig.Regulator.IsDriven);
    }

    [Fact]
    public void QueueOutlivesAnotherQueueEnding()
    {
        using var rig = new PaceRig();
        rig.Regulator.BeginQueue(Demand);
        rig.Regulator.BeginQueue(Revision);

        rig.Regulator.EndQueue(Demand);
        var afterOne = rig.Regulator.IsDriven;
        rig.Regulator.EndQueue(Revision);

        Assert.Equal((true, false), (afterOne, rig.Regulator.IsDriven));
    }

    [Fact]
    public void PassOutlivesTheQueue()
    {
        using var rig = new PaceRig();
        rig.Regulator.BeginQueue(Demand);
        rig.Regulator.Begin(PassId);
        rig.DecideAfter(30, SignalLevel.Green);
        rig.DecideAfter(30, SignalLevel.Green);

        rig.Regulator.EndQueue(Demand);

        Assert.True(rig.Regulator.IsDriven);
        Assert.Equal(PassId, rig.Regulator.Snapshot()!.PassId);
        Assert.Equal(60, rig.Regulator.Allowance());
    }

    [Fact]
    public async Task DrainWithoutAPassRendersAtTheFloorThenLetsThePaceGo()
    {
        await using var rig = await DemandRig.StartAsync(postgres, new Dictionary<string, string?>
        {
            ["LoDb:Prerender:Pace:Mode"] = "Adaptive",
            ["LoDb:Prerender:Pace:MinRendersPerMinute"] =
                PaceRig.Floor.ToString(CultureInfo.InvariantCulture),
        });
        foreach (var page in new[] { "champions/Aatrox", "items", "runes" })
        {
            rig.Refused.Refuse(DemandRig.PathOf(page));
        }

        await rig.CollectAsync();
        await rig.DrainAsync();

        Assert.All(await rig.RowsAsync(), static row =>
            Assert.Equal(PrerenderDemandState.Done, row.State));
        var floorWaits = rig.Harness.Clock.Waits.Count(static wait =>
            Math.Abs((wait - FloorInterval).TotalSeconds) < Tolerance);
        Assert.InRange(floorWaits, 1, 2);
        Assert.False(rig.Harness.Get<PaceRegulator>().IsDriven);
    }
}
