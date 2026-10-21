using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// Every source of the regulator, scripted: a host busy at <see cref="HostBusy"/>, an API never
/// throttled, two calm SSRs unless the test says otherwise; each read counted.
/// </summary>
internal sealed class FakePaceSources : IHostCpuReader, ICgroupCpuReader, ISsrLoadReader
{
    private const long TicksPerSample = 1000;

    private long _idle;
    private long _steal;
    private long _total;

    /// <summary>The host's busy share, steal included.</summary>
    public double HostBusy { get; set; } = 0.2;

    /// <summary>The share the hypervisor steals, within <see cref="HostBusy"/>.</summary>
    public double HostSteal { get; set; }

    /// <summary>The visitors' SSR load; null reads as unreachable.</summary>
    public SsrLoad? Visitors { get; set; } = LoadOf(renders: 0, refused: 0);

    public SsrLoad? Pass { get; set; } = LoadOf(renders: 0, refused: 0);

    public int HostReads { get; private set; }

    public int CgroupReads { get; private set; }

    public int LoadReads { get; private set; }

    public static SsrLoad LoadOf(long renders, long refused)
    {
        var idle = new SsrLaneLoad { Renders = 0, RenderSeconds = 0, Refused = 0 };
        return new SsrLoad
        {
            CpuSeconds = 1,
            EventLoopDelayP99Seconds = 0.012,
            CpuPressureSomeAvg10 = 0.001,
            Lanes = new SsrLanes
            {
                Current = idle with
                {
                    Renders = renders,
                    RenderSeconds = renders * 0.125,
                    Refused = refused,
                },
                Pinned = idle,
                Prerender = idle,
            },
        };
    }

    public Task<HostCpuSample?> ReadAsync(CancellationToken cancellationToken)
    {
        HostReads++;
        _total += TicksPerSample;
        _idle += (long)Math.Round(TicksPerSample * (1 - HostBusy));
        _steal += (long)Math.Round(TicksPerSample * HostSteal);
        return Task.FromResult<HostCpuSample?>(new HostCpuSample(_idle, _steal, _total));
    }

    Task<CgroupCpuSample?> ICgroupCpuReader.ReadAsync(CancellationToken cancellationToken)
    {
        CgroupReads++;
        return Task.FromResult<CgroupCpuSample?>(new CgroupCpuSample(CgroupReads * 1000L, 0));
    }

    public Task<SsrLoad?> ReadAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        LoadReads++;
        return Task.FromResult(url.Host == "lodb-web-ssr" ? Visitors : Pass);
    }
}
