using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Manual;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;
using LoDb.Api.Modules.Admin.Prerender.Pace.Resources;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Pace;

/// <summary>
/// The regulator's eyes, ticked every <see cref="PaceTiming.SamplePeriod"/> by
/// <c>PrerenderPaceWorker</c>: samples the host and the API's cgroup at each tick; every
/// third, reads the operator's setting again, both SSRs' load for the admin's resources view,
/// and, while a pass drains here, lets the regulator decide.
/// </summary>
/// <remarks>
/// Reads nothing in fixed mode, nor while the pass is not configured. Takes no advisory lock:
/// it only sets the memory of its own instance, which drains a pass under the pass's lock.
/// </remarks>
internal sealed class PaceSampler(
    PrerenderActivation activation,
    IOptions<PrerenderOptions> options,
    IHostCpuReader hostCpu,
    ICgroupCpuReader apiCgroup,
    SsrLoadsReader loads,
    PassRenderTimes renders,
    PaceRegulator regulator,
    PaceSourceWatch sources,
    PaceSettings settings,
    IPaceSettingSource stored,
    ResourceBoard resources,
    TimeProvider clock)
{
    private readonly HostWindow _host = new();
    private readonly ApiThrottleWindow _api = new(clock);
    private readonly PassLatencyBaseline _baseline = new();
    private SsrLoad? _visitorsBefore;
    private long _drive;
    private long _ticks;

    /// <summary>One tick of the sampler.</summary>
    public async Task TickAsync(CancellationToken cancellationToken)
    {
        if (!activation.IsActive || !options.Value.Pace.IsAdaptive)
        {
            return;
        }

        await SampleAsync(cancellationToken);
        if (++_ticks % PaceTiming.SamplesPerDecision != 0)
        {
            return;
        }

        settings.Apply(await stored.ReadAsync(cancellationToken)
            ?? PaceSetting.DefaultOf(options.Value.Pace));
        var read = await loads.ReadAsync(cancellationToken);
        resources.Note(read, _host.Shares(), new StackCpu(_host.Cpus, _api.Cores()));
        if (regulator.IsDriven)
        {
            Restart();
            regulator.Decide(Evaluate(read));
        }

        _visitorsBefore = read.Visitors;
    }

    private async Task SampleAsync(CancellationToken cancellationToken)
    {
        if (await hostCpu.ReadAsync(cancellationToken) is { } host)
        {
            _host.Add(host);
        }

        if (await apiCgroup.ReadAsync(cancellationToken) is { } api)
        {
            _api.Add(api);
        }
    }

    // A new drain forgets what the sampler read of the one before.
    private void Restart()
    {
        if (regulator.Drive == _drive)
        {
            return;
        }

        (_drive, _visitorsBefore) = (regulator.Drive, null);
        _baseline.Clear();
    }

    private PaceEvaluation Evaluate(SsrLoadSet read)
    {
        var stats = renders.Measure();
        var (visitors, pass) = (read.Visitors, read.WorstRenderer);
        var readings = new PaceReadings
        {
            Host = _host.Shares(),
            ApiThrottled = _api.Share(),
            Visitors = visitors is null ? null : SsrSignals.Of(_visitorsBefore, visitors),
            Pass = pass is null ? null : SsrSignals.Of(null, pass),
            PassLatency = LatencyOf(stats),
        };
        NoteSources(readings);
        return new PaceEvaluation(PaceBoard.Of(readings), clock.GetUtcNow(), stats.Starts);
    }

    // The current minute's median joins the baseline first: a baseline never above it.
    private PassLatencySample LatencyOf(PassRenderStats stats)
    {
        if (stats.Renders >= PaceThresholds.PassRendersNeeded && stats.MedianSeconds is { } median)
        {
            _baseline.Note(clock.GetUtcNow(), median);
        }

        return new PassLatencySample(stats.Renders, stats.MedianSeconds, _baseline.Lowest());
    }

    private void NoteSources(PaceReadings readings)
    {
        sources.Note(PaceSource.HostCpu, readings.Host.Busy is not null);
        sources.Note(PaceSource.ApiCgroup, readings.ApiThrottled is not null);
        sources.Note(PaceSource.VisitorsSsr, readings.Visitors is not null);
        sources.Note(PaceSource.PassSsr, readings.Pass is not null);
    }
}
