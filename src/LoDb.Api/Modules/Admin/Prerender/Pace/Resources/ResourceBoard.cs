using LoDb.Api.Modules.Admin.Prerender.Pace.Readings.Load;
using LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Resources;

/// <summary>
/// The resources the admin's speed setting shows, noted by <c>PaceSampler</c> every 30 s,
/// whether a pass drains here or not: the operator sets a pace before launching one too.
/// </summary>
/// <remarks>
/// In memory, as the regulator: another instance, or this one before its first notes, has
/// nothing to show. Nothing is noted while the pass is not configured or its pace is fixed.
/// </remarks>
internal sealed class ResourceBoard(TimeProvider clock)
{
    private const string VisitorsSource = "visitors";
    private const double SecondsPerMinute = 60;

    private readonly Lock _gate = new();
    private readonly CpuTally _renderers = new();
    private readonly CpuTally _visitors = new();
    private readonly CpuWindow _renderersWindow = new(clock);
    private readonly CpuWindow _visitorsWindow = new(clock);
    private SsrLoad? _visitorsBefore;
    private long _visitorsBeforeAt;
    private ResourceReadings? _readings;

    /// <summary>The latest readings; null before the first notes.</summary>
    public ResourceReadings? Readings
    {
        get
        {
            lock (_gate)
            {
                return _readings;
            }
        }
    }

    /// <summary>Notes one read of the SSRs, the host and the API.</summary>
    public void Note(SsrLoadSet loads, HostShares host, StackCpu stack)
    {
        ArgumentNullException.ThrowIfNull(loads);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(stack);
        lock (_gate)
        {
            foreach (var (source, load) in loads.Renderers)
            {
                _renderers.Add(source, load.CpuSeconds, load.Lanes.Prerender.Renders);
            }

            _renderersWindow.Add(_renderers);
            var (refused, olderRefused) = NoteVisitors(loads.Visitors);
            _readings = new ResourceReadings
            {
                At = clock.GetUtcNow(),
                HostCpus = stack.HostCpus,
                HostBusy = host.Busy,
                HostSteal = host.Steal,
                Renderers = loads.Renderers.Count,
                RenderersCores = _renderersWindow.Cores(),
                RenderersPerMinute = _renderersWindow.RendersPerMinute(),
                RendererCpuSecondsPerPage = _renderersWindow.CpuPerRender(
                    PageCost.RendersToMeasure),
                VisitorsCores = _visitorsWindow.Cores(),
                VisitorsRefused = refused,
                OlderRefusedPerMinute = olderRefused,
                ApiCores = stack.ApiCores,
            };
        }
    }

    // The visitors' refusals since the read before: a counter that went back says nothing.
    private (long? Refused, double? OlderPerMinute) NoteVisitors(SsrLoad? visitors)
    {
        if (visitors is null)
        {
            _visitorsBefore = null;
            return (null, null);
        }

        _visitors.Add(VisitorsSource, visitors.CpuSeconds, visitors.Lanes.Current.Renders);
        _visitorsWindow.Add(_visitors);
        var (before, beforeAt, now) = (_visitorsBefore, _visitorsBeforeAt, clock.GetTimestamp());
        (_visitorsBefore, _visitorsBeforeAt) = (visitors, now);
        if (before is null)
        {
            return (null, null);
        }

        var refused = visitors.Lanes.Current.Refused - before.Lanes.Current.Refused;
        var older = visitors.Lanes.Pinned.Refused - before.Lanes.Pinned.Refused;
        var minutes = clock.GetElapsedTime(beforeAt, now).TotalSeconds / SecondsPerMinute;
        return refused < 0 || older < 0 || minutes <= 0
            ? (null, null)
            : (refused, older / minutes);
    }
}
