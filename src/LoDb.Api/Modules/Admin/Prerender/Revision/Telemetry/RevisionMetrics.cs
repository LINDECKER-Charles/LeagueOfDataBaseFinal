using System.Diagnostics.Metrics;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Telemetry;

/// <summary>
/// The queue of the closed states on meter <c>LoDb.Prerender</c>:
/// <c>lodb.prerender.revision.enqueued</c> (<c>lodb_prerender_revision_enqueued_total</c>), the
/// rows its producer wrote, <c>lodb.prerender.revision.pages</c>
/// (<c>lodb_prerender_revision_pages_total{outcome}</c>) per page the drain ended or refusal it
/// waited out, and <c>lodb.prerender.revision.pending</c>
/// (<c>lodb_prerender_revision_pending</c>), the pages waiting, as last counted.
/// </summary>
/// <remarks>
/// The gauge reads a value the drain keeps, never the database: a scrape costs no query.
/// </remarks>
internal sealed class RevisionMetrics
{
    private readonly Counter<long> _enqueued;
    private readonly Counter<long> _pages;
    private long _pending;

    public RevisionMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(PrerenderMetrics.MeterName);
        _enqueued = meter.CreateCounter<long>(
            "lodb.prerender.revision.enqueued",
            unit: "{page}",
            description: "Pages of closed states enqueued to be rendered again, per epoch.");
        _pages = meter.CreateCounter<long>(
            "lodb.prerender.revision.pages",
            unit: "{page}",
            description: "Pages of the queue of the closed states, by outcome.");
        meter.CreateObservableGauge(
            "lodb.prerender.revision.pending",
            () => Interlocked.Read(ref _pending),
            unit: "{page}",
            description: "Pages waiting in the queue of the closed states.");
    }

    public void Enqueued(int rows)
    {
        if (rows > 0)
        {
            _enqueued.Add(rows);
        }
    }

    public void Count(PageOutcome outcome) => _pages.Add(
        1,
        new KeyValuePair<string, object?>(PrerenderMetrics.OutcomeTag, TagOf(outcome)));

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
