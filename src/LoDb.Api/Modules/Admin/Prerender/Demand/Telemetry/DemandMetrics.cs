using System.Diagnostics.Metrics;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Telemetry;

/// <summary>
/// The automatic queue on meter <c>LoDb.Prerender</c>: <c>lodb.prerender.demand.pages</c>
/// (<c>lodb_prerender_demand_pages_total{outcome}</c>) per page the worker ended or refusal it
/// waited out, <c>lodb.prerender.demand.captured</c>
/// (<c>lodb_prerender_demand_captured_total{result}</c>) per refused page read from the SSR,
/// and <c>lodb.prerender.demand.pending</c> (<c>lodb_prerender_demand_pending</c>), the pages
/// waiting, as last counted.
/// </summary>
/// <remarks>
/// The gauge reads a value the workers keep, never the database: a scrape costs no query.
/// </remarks>
internal sealed class DemandMetrics
{
    private const string ResultTag = "result";

    private readonly Counter<long> _pages;
    private readonly Counter<long> _captured;
    private long _pending;

    public DemandMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(PrerenderMetrics.MeterName);
        _pages = meter.CreateCounter<long>(
            "lodb.prerender.demand.pages",
            unit: "{page}",
            description: "Pages of the automatic queue, by outcome.");
        _captured = meter.CreateCounter<long>(
            "lodb.prerender.demand.captured",
            unit: "{page}",
            description: "Refused pages read from the SSR: queued, rejected or deferred.");
        meter.CreateObservableGauge(
            "lodb.prerender.demand.pending",
            () => Interlocked.Read(ref _pending),
            unit: "{page}",
            description: "Pages waiting in the automatic queue.");
    }

    public void Count(PageOutcome outcome, int times = 1) => _pages.Add(
        times,
        new KeyValuePair<string, object?>(PrerenderMetrics.OutcomeTag, TagOf(outcome)));

    public void Captured(string result, int pages)
    {
        if (pages > 0)
        {
            _captured.Add(pages, new KeyValuePair<string, object?>(ResultTag, result));
        }
    }

    public void Pending(long pages) => Interlocked.Exchange(ref _pending, pages);

    private static string TagOf(PageOutcome outcome) => outcome switch
    {
        PageOutcome.Written => "written",
        PageOutcome.Skipped => "skipped",
        PageOutcome.Refused => "refused",
        PageOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
