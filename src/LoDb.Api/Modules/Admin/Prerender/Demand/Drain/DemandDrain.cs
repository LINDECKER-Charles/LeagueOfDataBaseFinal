using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Demand.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Infrastructure.Locks;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Drain;

/// <summary>
/// The automatic queue at work: batch after batch of its most requested pending pages, written
/// into the served epoch, until none is left or a page stops it; one summary line per batch.
/// </summary>
/// <remarks>
/// <para>
/// One instance drains at a time, under an advisory lock of its own: a manual pass runs beside
/// it, under its own, and both share the pass SSR's renders (<see cref="Sharing.RenderSlots"/>)
/// and its pace. While it has pages, the queue drives the <see cref="PaceRegulator"/> too:
/// alone, it renders at the pace the regulator or the operator sets, the host and the visitors
/// watched, never at the fixed pace no signal watches. Dormant while the queue is switched off or the store is not configured
/// (<see cref="DemandActivation"/>).
/// </para>
/// <para>
/// At its first drain, after a drain that failed, and whenever the served epoch changed, the
/// pages a stopped worker left rendering and those ended in another epoch go back to pending.
/// </para>
/// </remarks>
internal sealed class DemandDrain(
    DemandActivation activation,
    IDistributedLock locks,
    DemandQueue queue,
    DemandPageRunner pages,
    DemandMetrics metrics,
    PaceRegulator regulator,
    IOptions<PrerenderOptions> options,
    TimeProvider clock,
    ILogger<DemandDrain> logger)
{
    /// <summary>The advisory lock of whoever drains the queue.</summary>
    public const string LockName = "prerender:demand";

    /// <summary>Pages a batch takes: under a minute of renders at the pass's floor.</summary>
    public const int BatchSize = 20;

    private const string NoReason = "none.";

    private int? _revivedEpoch;
    private string? _stoppedBy;

    /// <summary>Drains the queue, unless it is off or another instance drains it.</summary>
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

        var epoch = options.Value.ServedEpoch;
        if (_revivedEpoch != epoch)
        {
            await queue.ReviveAsync(epoch, cancellationToken);
            _revivedEpoch = epoch;
        }

        // Cleared only by a drain that ended well: one that threw may have left pages
        // rendering, which the next one puts back.
        _revivedEpoch = null;
        await DrainAsync(epoch, cancellationToken);
        _revivedEpoch = epoch;
    }

    private async Task DrainAsync(int epoch, CancellationToken cancellationToken)
    {
        try
        {
            await DrainBatchesAsync(epoch, cancellationToken);
        }
        finally
        {
            regulator.EndQueue(LockName);
        }
    }

    private async Task DrainBatchesAsync(int epoch, CancellationToken cancellationToken)
    {
        while (await queue.NextAsync(BatchSize, cancellationToken) is { Count: > 0 } rows)
        {
            regulator.BeginQueue(LockName);
            var started = clock.GetTimestamp();
            var tally = await RunBatchAsync(rows, epoch, cancellationToken);
            Log(tally, epoch, (long)clock.GetElapsedTime(started).TotalMilliseconds);
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

    // As many pages at once as the pass's SSR admits: the slots hold the queue to its share.
    private async Task<DemandTally> RunBatchAsync(
        IReadOnlyList<DemandRow> rows,
        int epoch,
        CancellationToken cancellationToken)
    {
        var tally = new DemandTally();
        var parallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Value.MaxInFlight,
            CancellationToken = cancellationToken,
        };
        await Parallel.ForEachAsync(rows, parallel, async (row, token) =>
        {
            if (!tally.IsStopped && await pages.RunAsync(row, epoch, token) is { } end)
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
            DemandLog.Stopped(logger, reason);
        }

        _stoppedBy = reason;
    }

    private void Log(DemandTally tally, int epoch, long durationMs) =>
        DemandLog.Drained(
            logger,
            tally.Pages,
            epoch,
            tally.Written,
            tally.Skipped,
            tally.Failed,
            tally.Deferred,
            tally.Bytes,
            tally.Refusals,
            durationMs,
            tally.LastReason ?? NoReason);
}
