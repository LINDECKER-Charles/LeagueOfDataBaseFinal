using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;
using LoDb.Infrastructure.PageStore;
using Microsoft.Extensions.Logging.Testing;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// The sampler: the host and the API every tick, both SSRs and a decision every third while a
/// pass drains here; nothing in fixed mode; a source lost said once, and read as missing.
/// </summary>
public sealed class PaceSamplerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FixedModeReadsNothing()
    {
        using var rig = new PaceRig(PaceMode.Fixed);
        var (sampler, sources) = Sampler(rig);
        rig.Regulator.Begin(1);

        await TicksAsync(rig, sampler, 6);

        Assert.Equal((0, 0, 0), (sources.HostReads, sources.CgroupReads, sources.LoadReads));
    }

    [Fact]
    public async Task HostIsSampledEveryTickAndTheSsrsOnlyToDecide()
    {
        using var rig = new PaceRig();
        var (sampler, sources) = Sampler(rig);

        await TicksAsync(rig, sampler, 3);
        var idleLoads = sources.LoadReads;
        rig.Regulator.Begin(1);
        await TicksAsync(rig, sampler, 3);

        Assert.Equal((6, 6), (sources.HostReads, sources.CgroupReads));
        Assert.Equal((0, 2), (idleLoads, sources.LoadReads));
        var snapshot = rig.Regulator.Snapshot()!;
        Assert.Equal(PaceState.Starting, snapshot.State);
        Assert.Equal(0.2, snapshot.HostBusy!.Value, 6);
    }

    [Fact]
    public async Task BusyHostSlowsThePassAtTheNextDecision()
    {
        using var rig = new PaceRig();
        var (sampler, sources) = Sampler(rig);
        rig.Regulator.Begin(1);
        (sources.HostBusy, sources.HostSteal) = (0.6, 0.2);

        await TicksAsync(rig, sampler, 9);

        var snapshot = rig.Regulator.Snapshot()!;
        Assert.Equal((PaceState.Slowed, PaceReason.HostCpu), (snapshot.State, snapshot.Reason));
    }

    [Fact]
    public async Task LostSourceSlowsThePassAndIsSaidOnceUntilItReturns()
    {
        using var rig = new PaceRig();
        var (sampler, sources) = Sampler(rig);
        rig.Regulator.Begin(1);
        sources.Pass = null;

        await TicksAsync(rig, sampler, 9);
        var lost = rig.Regulator.Snapshot()!;
        sources.Pass = FakePaceSources.LoadOf(0, 0);
        await TicksAsync(rig, sampler, 6);

        Assert.Equal((PaceState.Slowed, PaceReason.SignalMissing, PaceRig.Floor),
            (lost.State, lost.Reason, lost.RendersPerMinute));
        Assert.Equal(
            ["prerender.pace.signal_lost", "prerender.pace.signal_restored"],
            Names(rig).Where(static name => name.Contains("signal", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task RefusedVisitorHoldsThePassAndARestartedSsrReadsAsMissing()
    {
        using var rig = new PaceRig();
        var (sampler, sources) = Sampler(rig);
        rig.Regulator.Begin(1);
        sources.Visitors = FakePaceSources.LoadOf(renders: 100, refused: 0);
        await TicksAsync(rig, sampler, 3);
        sources.Visitors = FakePaceSources.LoadOf(renders: 110, refused: 1);
        await TicksAsync(rig, sampler, 3);
        var refused = rig.Regulator.Snapshot()!;

        sources.Visitors = FakePaceSources.LoadOf(renders: 2, refused: 0);
        await TicksAsync(rig, sampler, 3);

        Assert.Equal((PaceState.Held, PaceReason.VisitorsRefused), (refused.State, refused.Reason));
        Assert.Contains("prerender.pace.held", Names(rig));
        Assert.Equal(PaceState.Held, rig.Regulator.Snapshot()!.State);
    }

    private static IEnumerable<string> Names(PaceRig rig) => rig.EventNames;

    private static async Task TicksAsync(PaceRig rig, PaceSampler sampler, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            rig.Clock.Advance(PaceTiming.SamplePeriod);
            await sampler.TickAsync(Token);
        }
    }

    private static (PaceSampler Sampler, FakePaceSources Sources) Sampler(PaceRig rig)
    {
        var sources = new FakePaceSources();
        var store = new PageStoreOptions
        {
            Endpoint = "https://s3.fr-par.scw.cloud",
            Bucket = "lodb-pages-test",
            AccessKey = "writer",
            SecretKey = "writer-secret",
        };
        var options = Microsoft.Extensions.Options.Options.Create(rig.Options);
        var sampler = new PaceSampler(
            new PrerenderActivation(options, Microsoft.Extensions.Options.Options.Create(store)),
            options,
            sources,
            sources,
            sources,
            new PassRenderTimes(rig.Clock),
            rig.Regulator,
            new PaceSourceWatch(rig.Clock, new FakeLogger<PaceSourceWatch>(rig.Logs)),
            rig.Clock);
        return (sampler, sources);
    }
}
