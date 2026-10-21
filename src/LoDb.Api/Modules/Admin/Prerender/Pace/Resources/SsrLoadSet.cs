using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Resources;

/// <summary>
/// One read of every SSR's load: the visitors' (null when unreadable), and each renderer of
/// the pass that answered, by its address (or by its name, with a single renderer).
/// </summary>
internal sealed record SsrLoadSet(
    SsrLoad? Visitors,
    IReadOnlyDictionary<string, SsrLoad> Renderers)
{
    /// <summary>
    /// The renderer the regulator reads, the one under the most pressure; null when none
    /// answered.
    /// </summary>
    public SsrLoad? WorstRenderer =>
        Renderers.Values.MaxBy(static load => load.CpuPressureSomeAvg10 ?? 0);
}
