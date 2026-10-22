using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Demand.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Demand.Validation;
using LoDb.Api.Modules.Admin.Prerender.Options;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Capture;

/// <summary>
/// Brings the pages the visitors' SSR refused into the queue: reads what changed since its
/// cursor, keeps the pages of the catalogue, adds their counts. One summary line per read that
/// brought pages.
/// </summary>
/// <remarks>
/// <para>
/// The SSR's counts are cumulative and the table adds their increase
/// (<see cref="DemandUpserts"/>): reading the same pages twice, from two instances of the API or
/// after its restart from the start, counts nothing twice. The cursor lives here, in memory; a
/// new SSR instance is read from the start, its counts starting from zero.
/// </para>
/// <para>
/// <see cref="MostReadsPerTick"/> answers at most per tick: a crawl's backlog comes in over a
/// few ticks rather than in one long one. An SSR out of reach is said once, then once again
/// when it answers.
/// </para>
/// </remarks>
internal sealed class DemandCollector(
    DemandActivation activation,
    IRefusedPagesReader reader,
    DemandValidator validator,
    DemandUpserts upserts,
    DemandQueue queue,
    DemandMetrics metrics,
    IOptions<PrerenderOptions> options,
    ILogger<DemandCollector> logger)
{
    /// <summary>Answers read per tick, some ten thousand pages each.</summary>
    public const int MostReadsPerTick = 10;

    private string? _instance;
    private long _cursor;
    private bool _isUnreadable;

    /// <summary>One tick: the SSR's new refusals, into the queue.</summary>
    public async Task CollectAsync(CancellationToken cancellationToken)
    {
        if (activation.RefusedPagesUrl is not { } url)
        {
            return;
        }

        var (read, queued, rejected, deferred) = (0, 0, 0, 0);
        for (var reads = 0; reads < MostReadsPerTick; reads++)
        {
            if (await ReadAsync(url, cancellationToken) is not { } report)
            {
                break;
            }

            var judged = await AddAsync(report, cancellationToken);
            (read, queued) = (read + report.Pages.Count, queued + judged.Accepted.Count);
            (rejected, deferred) = (rejected + judged.Rejected, deferred + judged.Deferred);
            if (report.IsComplete)
            {
                break;
            }
        }

        await NoteAsync(new JudgedCounts(read, queued, rejected, deferred), cancellationToken);
    }

    // The cursor moves on once the pages are in the table: a failure reads them again.
    private async Task<JudgedPages> AddAsync(
        RefusedPagesReport report,
        CancellationToken cancellationToken)
    {
        var judged = await validator.JudgeAsync(report.Pages, cancellationToken);
        await upserts.UpsertAsync(
            report.Instance,
            options.Value.ServedEpoch,
            judged.Accepted,
            cancellationToken);
        (_instance, _cursor) = (report.Instance, report.Cursor);
        return judged;
    }

    // A new SSR instance counts from zero: the cursor of the one before would skip its first
    // pages.
    private async Task<RefusedPagesReport?> ReadAsync(Uri url, CancellationToken cancellationToken)
    {
        var report = await reader.ReadAsync(url, _cursor, cancellationToken);
        if (report is not null && report.Instance != _instance && _cursor > 0)
        {
            _cursor = 0;
            report = await reader.ReadAsync(url, _cursor, cancellationToken);
        }

        NoteReach(url, report is not null);
        return report;
    }

    private void NoteReach(Uri url, bool isRead)
    {
        if (isRead == !_isUnreadable)
        {
            return;
        }

        _isUnreadable = !isRead;
        if (isRead)
        {
            DemandLog.Readable(logger, url);
        }
        else
        {
            DemandLog.Unreadable(logger, url);
        }
    }

    private async Task NoteAsync(JudgedCounts counts, CancellationToken cancellationToken)
    {
        if (counts.Read == 0)
        {
            return;
        }

        metrics.Captured("queued", counts.Queued);
        metrics.Captured("rejected", counts.Rejected);
        metrics.Captured("deferred", counts.Deferred);
        metrics.Pending(await queue.PendingAsync(cancellationToken));
        DemandLog.Collected(logger, counts.Read, counts.Queued, counts.Rejected, counts.Deferred);
    }

    private sealed record JudgedCounts(int Read, int Queued, int Rejected, int Deferred);
}
