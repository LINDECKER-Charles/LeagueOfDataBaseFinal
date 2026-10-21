namespace LoDb.Api.Modules.Admin.Prerender.Http.Speed;

/// <summary>
/// The host's resources as this instance last read them, every 30 s: shares in 0..1 and cores,
/// each null while it cannot be told. Counts and CPU only, nothing of a visitor.
/// </summary>
internal sealed record PrerenderResourcesView
{
    /// <summary>When they were read.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The host's CPUs.</summary>
    public int? HostCpus { get; init; }

    /// <summary>The host's busy share over the last minute, steal counted as busy.</summary>
    public decimal? HostBusy { get; init; }

    /// <summary>The share the hypervisor stole over the last minute, within the busy one.</summary>
    public decimal? HostSteal { get; init; }

    /// <summary>The renderers that answered.</summary>
    public required int Renderers { get; init; }

    /// <summary>Cores the renderers used over the last minute.</summary>
    public decimal? RenderersCores { get; init; }

    /// <summary>Renders the renderers ended a minute, over the last minute.</summary>
    public decimal? RenderersPerMinute { get; init; }

    /// <summary>Cores the visitors' SSR used over the last minute.</summary>
    public decimal? VisitorsCores { get; init; }

    /// <summary>
    /// Visitors of the latest patch refused over the last 30 s: the brake's sign.
    /// </summary>
    public long? VisitorsRefused { get; init; }

    /// <summary>
    /// Older-patch pages the visitors' SSR refused a minute (503), over the last 30 s.
    /// </summary>
    public decimal? OlderRefusedPerMinute { get; init; }

    /// <summary>Cores the API used over the last minute.</summary>
    public decimal? ApiCores { get; init; }
}
