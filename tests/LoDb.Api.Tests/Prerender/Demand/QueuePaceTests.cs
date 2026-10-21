using LoDb.Api.Modules.Admin.Prerender.Demand.Sharing;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Pace.Manual;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Prerender.Demand;

/// <summary>
/// Without a pass to drive the regulator, the queue keeps to the adaptive pace's floor rather
/// than the fixed pace no signal watches; beside a pass, or in fixed mode, it waits nothing more.
/// </summary>
public sealed class QueuePaceTests(PostgresContainerFixture postgres)
{
    private const int Floor = 30;
    private const double Tolerance = 0.05;
    private static readonly TimeSpan FloorInterval = TimeSpan.FromMinutes(1) / Floor;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueueAloneIsSpacedAtTheFloor()
    {
        var clock = new InstantClock();
        var (pace, _) = Pace(PaceMode.Adaptive, clock);

        await pace.WaitTurnAsync(Token);
        await pace.WaitTurnAsync(Token);
        await pace.WaitTurnAsync(Token);

        Assert.Equal(2, clock.Waits.Count);
        Assert.All(clock.Waits, static wait =>
            Assert.Equal(FloorInterval.TotalSeconds, wait.TotalSeconds, Tolerance));
    }

    [Fact]
    public async Task PassDrivingTheRegulatorSetsThePaceAlone()
    {
        var clock = new InstantClock();
        var (pace, regulator) = Pace(PaceMode.Adaptive, clock);
        regulator.Begin(1);

        await pace.WaitTurnAsync(Token);
        await pace.WaitTurnAsync(Token);

        Assert.Empty(clock.Waits);
    }

    [Fact]
    public async Task FixedModeKeepsTheOperatorsPace()
    {
        var clock = new InstantClock();
        var (pace, _) = Pace(PaceMode.Fixed, clock);

        await pace.WaitTurnAsync(Token);
        await pace.WaitTurnAsync(Token);

        Assert.Empty(clock.Waits);
    }

    [Fact]
    public async Task DrainWithoutAPassKeepsToTheFloor()
    {
        await using var rig = await DemandRig.StartAsync(postgres, new Dictionary<string, string?>
        {
            ["LoDb:Prerender:Pace:Mode"] = "Adaptive",
            ["LoDb:Prerender:Pace:MinRendersPerMinute"] = Floor.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
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
    }

    private static (QueuePace Pace, PaceRegulator Regulator) Pace(PaceMode mode, TimeProvider clock)
    {
        var options = Options.Create(new PrerenderOptions
        {
            Pace = new PaceOptions { Mode = mode, MinRendersPerMinute = Floor },
        });
        using var meters = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var regulator = new PaceRegulator(
            options,
            new PaceSettings(options, NullLogger<PaceSettings>.Instance),
            clock,
            new PaceMetrics(meters.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>()),
            NullLogger<PaceRegulator>.Instance);
        return (new QueuePace(regulator, options, clock), regulator);
    }
}
