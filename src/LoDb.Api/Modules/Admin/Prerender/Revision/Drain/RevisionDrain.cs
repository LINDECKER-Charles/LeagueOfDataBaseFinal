using LoDb.Api.Modules.Admin.Prerender.Demand.Drain;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Revision.Queue;
using LoDb.Api.Modules.Admin.Prerender.Revision.Telemetry;
using LoDb.Infrastructure.Locks;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Drain;

/// <summary>
/// The queue of the closed states at work: batch after batch of its pages that are due, each
/// written into its row's epoch, until none is left or a page stops it; one summary line per
/// batch.
/// </summary>
/// <remarks>
/// <para>
/// One instance drains at a time, under an advisory lock of its own, beside a manual pass and
/// the automatic queue of the refused pages: the three share the pass SSR's renders (the
/// queues' share of <c>RenderSlots</c>) and its pace, which it drives while it has pages, as
/// the automatic queue does. Dormant while the store is not configured; it never depends on
/// <c>AutoEnqueueOnPromotion</c>.
/// </para>
/// <para>
/// At its first drain, and after a drain that failed, the pages a stopped worker left
/// rendering go back to pending.
/// </para>
/// </remarks>
internal sealed class RevisionDrain(
    PrerenderActivation activation,
    IDistributedLock locks,
    RevisionQueue queue,
    RevisionPageRunner pages,
    RevisionMetrics metrics,
    PaceRegulator regulator,
    IOptions<PrerenderOptions> options,
    TimeProvider clock,
    ILogger<RevisionDrain> logger)
{
    /// <summary>The advisory lock of whoever drains the queue.</summary>
    public const string LockName = "prerender:revision";

    /// <summary>Pages a batch takes, as the automatic queue's.</summary>
    public const int BatchSize = DemandDrain.BatchSize;

    private const string NoReason = "none.";

    private bool _isRevived;
    private string? _stoppedBy;

    /// <summary>
    /// Drains the queue, unless the store is not configured or another instance drains it.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!activation.IsActive)
        {
            return;
        }

        await using var held = await locks.TryAcquireAsync(LockName, cancellationToken);
        if (held is null)
        {
            return;
        }

        if (!_isRevived)
        {
            await queue.ReviveAsync(cancellationToken);
        }

        // Cleared only by a drain that ended well: one that threw may have left pages
        // rendering, which the next one puts back.
        _isRevived = false;
        await DrainAsync(cancellationToken);
        _isRevived = true;
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DrainBatchesAsync(cancellationToken);
        }
        finally
        {
            regulator.EndQueue(LockName);
        }
    }

    private async Task DrainBatchesAsync(CancellationToken cancellationToken)
    {
        while (await queue.NextAsync(BatchSize, cancellationToken) is { Count: > 0 } rows)
        {
            regulator.BeginQueue(LockName);
            var started = clock.GetTimestamp();
            var tally = await RunBatchAsync(rows, cancellationToken);
            Log(tally, (long)clock.GetElapsedTime(started).TotalMilliseconds);
            metrics.Pending(await queue.PendingAsync(cancellationToken));
            if (tally.StoppedBy is { } reason)
            {
                NoteStop(reason);
                return;
            }

            _stoppedBy = null;
        }

        metrics.Pending(await queue.PendingAsync(cancellationToken));
    }

    // As many pages at once as the pass's SSR admits: the slots hold the queues to their share.
    private async Task<DemandTally> RunBatchAsync(
        IReadOnlyList<RevisionRow> rows,
        CancellationToken cancellationToken)
    {
        var tally = new DemandTally();
        var served = options.Value.ServedEpoch;
        var parallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Value.MaxInFlight,
            CancellationToken = cancellationToken,
        };
        await Parallel.ForEachAsync(rows, parallel, async (row, token) =>
        {
            if (!tally.IsStopped && await pages.RunAsync(row, served, token) is { } end)
            {
                tally.Add(end);
            }
        });
        return tally;
    }

    // Said once per cause, as the pass says an outage: an SSR left down stops every tick.
    private void NoteStop(string reason)
    {
        if (reason != _stoppedBy)
        {
            RevisionLog.Stopped(logger, reason);
        }

        _stoppedBy = reason;
    }

    private void Log(DemandTally tally, long durationMs) =>
        RevisionLog.Drained(
            logger,
            tally.Pages,
            tally.Written,
            tally.Skipped,
            tally.Failed,
            tally.Deferred,
            tally.Bytes,
            tally.Refusals,
            durationMs,
            tally.LastReason ?? NoReason);
}
