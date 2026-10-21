using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Api.Tests.Prerender.Support;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// The pacer spaces the pass's renders at the allowance in force, read at each reservation;
/// held, it reserves nothing and lets the page look again whether its pass runs, uncounted.
/// </summary>
public sealed class RenderPacerTests(PostgresContainerFixture postgres)
{
    private static readonly PatchVersion Version = PatchVersion.Parse("16.10.1");
    private const double Tolerance = 0.05;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RendersAreSpacedByTheAllowanceInForce()
    {
        var clock = new InstantClock();
        var (pacer, regulator, _) = Pacer(clock);

        await pacer.WaitTurnAsync(Token);
        await pacer.WaitTurnAsync(Token);
        regulator.Begin(1);
        await pacer.WaitTurnAsync(Token);
        await pacer.WaitTurnAsync(Token);

        var waits = clock.Waits.Select(static wait => wait.TotalSeconds).ToList();
        Assert.Equal(3, waits.Count);
        Assert.Equal(0.5, waits[0], Tolerance);
        Assert.Equal(0.5, waits[1], Tolerance);
        Assert.Equal(2, waits[2], Tolerance);
    }

    [Fact]
    public async Task HeldPassReservesNothingAndLooksAgainWithinTenSeconds()
    {
        var clock = new InstantClock();
        var (pacer, regulator, renders) = Pacer(clock);
        regulator.Begin(1);
        regulator.Decide(PaceRig.Evaluation(SignalLevel.Red, clock.GetUtcNow(), 0));

        var started = await pacer.WaitTurnAsync(Token);

        Assert.False(started);
        Assert.Equal([PaceTiming.HeldRecheck], clock.Waits);
        Assert.Equal(0, renders.Measure().Starts);
    }

    [Fact]
    public async Task HeldPagesCountNothingAndResumeAtTheFloorOnceTheHoldGoesStale()
    {
        await using var harness = await AdaptiveAsync();
        var passId = await harness.LaunchAsync(Scope());
        var held = false;
        harness.Ssr.OnPage = _ =>
        {
            if (!held)
            {
                held = true;
                harness.Get<PaceRegulator>().Decide(
                    PaceRig.Evaluation(SignalLevel.Red, harness.Clock.GetUtcNow(), 0));
            }

            return Task.CompletedTask;
        };

        await harness.RunAsync();

        var pass = await harness.PassAsync(passId);
        Assert.Equal((PrerenderPassState.Completed, 6L), (pass.State, pass.PagesWritten));
        var unit = Assert.Single(pass.Units);
        Assert.Equal((0, 0), (unit.PagesSkipped, unit.PagesFailed));
        var rechecks = harness.Clock.Waits.Count(static wait => wait == PaceTiming.HeldRecheck);
        Assert.InRange(rechecks, 9, 11);
        var completed = harness.Logs.GetSnapshot()
            .Single(static entry => entry.Id.Name == "prerender.unit.completed");
        Assert.Equal(
            "0",
            completed.StructuredState!.Single(static field => field.Key == "Refusals").Value);
    }

    private static PassScope Scope() => PassScope.Of(Version) with { Locales = [UiLocale.En] };

    // The pass's regulator in adaptive mode with no sampler: the floor, until a test decides.
    private async Task<PrerenderHarness> AdaptiveAsync()
    {
        var harness = await PrerenderHarness.StartAsync(
            postgres,
            settings: new Dictionary<string, string?>
            {
                ["LoDb:Prerender:Pace:Mode"] = "Adaptive",
                ["LoDb:Prerender:Pace:MinRendersPerMinute"] = "6000",
            });
        harness.Catalog.Pinned.Add(Version);
        return harness;
    }

    // At a fixed pace of 120 a minute, a floor of 30 once a pass drains.
    private static (RenderPacer Pacer, PaceRegulator Regulator, PassRenderTimes Renders) Pacer(
        InstantClock clock)
    {
        var options = new PrerenderOptions { RendersPerMinute = PaceRig.FixedPace };
        using var meters = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var regulator = new PaceRegulator(
            Microsoft.Extensions.Options.Options.Create(options),
            clock,
            new PaceMetrics(meters.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>()),
            NullLogger<PaceRegulator>.Instance);
        var renders = new PassRenderTimes(clock);
        return (new RenderPacer(regulator, renders, clock), regulator, renders);
    }
}
