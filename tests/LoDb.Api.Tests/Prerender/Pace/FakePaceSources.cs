using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Pace.Manual;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings;
using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;
using LoDb.Api.Modules.Admin.Prerender.Rendering.Renderers;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// Every source of the regulator, scripted: a host busy at <see cref="HostBusy"/>, an API never
/// throttled, two calm SSRs unless the test says otherwise; each read counted.
/// </summary>
internal sealed class FakePaceSources
    : IHostCpuReader, ICgroupCpuReader, ISsrLoadReader, IPaceSettingSource, IRendererDns
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

    /// <summary>The stored setting; null as on a stack where none was ever set.</summary>
    public PaceSetting? Stored { get; set; }

    public int SettingReads { get; private set; }

    /// <summary>The renderers' addresses the DNS answers.</summary>
    public IReadOnlyList<IPAddress> Renderers { get; set; } = [];

    /// <summary>Each renderer's load by address; a missing one reads as unreachable.</summary>
    public Dictionary<string, SsrLoad> RendererLoads { get; } = new(StringComparer.Ordinal);

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
        if (IPAddress.TryParse(url.Host, out _))
        {
            return Task.FromResult(RendererLoads.GetValueOrDefault(url.Host));
        }

        return Task.FromResult(url.Host == "lodb-web-ssr" ? Visitors : Pass);
    }

    Task<PaceSetting?> IPaceSettingSource.ReadAsync(CancellationToken cancellationToken)
    {
        SettingReads++;
        return Task.FromResult(Stored);
    }

    public Task<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken) => Task.FromResult(Renderers);
}
