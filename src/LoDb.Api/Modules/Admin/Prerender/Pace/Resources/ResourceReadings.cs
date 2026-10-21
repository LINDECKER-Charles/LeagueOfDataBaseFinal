namespace LoDb.Api.Modules.Admin.Prerender.Pace.Resources;

/// <summary>
/// The host's resources as the sampler last read them, every 30 s: what the admin's speed
/// setting shows beside the control. Each figure is null while it cannot be told.
/// </summary>
internal sealed record ResourceReadings
{
    public required DateTimeOffset At { get; init; }

    /// <summary>The host's CPUs, from <c>/proc/stat</c>.</summary>
    public int? HostCpus { get; init; }

    /// <summary>The host's busy share over the last minute, steal included (0..1).</summary>
    public double? HostBusy { get; init; }

    /// <summary>The share the hypervisor stole over the last minute (0..1).</summary>
    public double? HostSteal { get; init; }

    /// <summary>The renderers of the pass that answered.</summary>
    public required int Renderers { get; init; }

    /// <summary>Cores the renderers used over the last minute, all together.</summary>
    public double? RenderersCores { get; init; }

    /// <summary>Renders the renderers ended over the last minute, a minute.</summary>
    public double? RenderersPerMinute { get; init; }

    /// <summary>A renderer's CPU seconds per render over 10 minutes; null while too few.</summary>
    public double? RendererCpuSecondsPerPage { get; init; }

    /// <summary>Cores the visitors' SSR used over the last minute.</summary>
    public double? VisitorsCores { get; init; }

    /// <summary>Visitors the latest-patch lane refused over the last 30 s.</summary>
    public long? VisitorsRefused { get; init; }

    /// <summary>
    /// Older-patch renders the visitors' SSR refused a minute, over the last 30 s.
    /// </summary>
    public double? OlderRefusedPerMinute { get; init; }

    /// <summary>Cores the API used over the last minute.</summary>
    public double? ApiCores { get; init; }
}
