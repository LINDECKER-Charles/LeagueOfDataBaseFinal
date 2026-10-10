using LoDb.Ingestion.Pipeline;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Queue;

/// <summary>
/// The slots the on-demand ingestions of an instance run in
/// (<c>LoDb:Ingestion:SyncConcurrency</c>), and the ingestions waiting for one.
/// </summary>
/// <remarks>
/// The work runs detached from the request that started it: an unbounded wait would pile up
/// work for clients long gone. A visitor's ingestion therefore waits a short while at most
/// (<c>SlotWait</c>), behind a bounded number of others (<c>MaxWaitingIngestions</c>); the
/// background worker, alone in its queue, waits as long as it takes.
/// </remarks>
internal sealed class SyncSlots : IDisposable
{
    public const string BacklogReason = "backlog";
    public const string SlotWaitReason = "slot_wait";

    private readonly SemaphoreSlim slots;
    private readonly int maxWaiting;
    private readonly TimeSpan slotWait;
    private readonly TimeProvider timeProvider;
    private readonly IngestionMetrics metrics;
    private int waiting;

    public SyncSlots(
        IOptions<IngestionOptions> options,
        TimeProvider timeProvider,
        IngestionMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        slots = new SemaphoreSlim(options.Value.SyncConcurrency);
        maxWaiting = options.Value.MaxWaitingIngestions;
        slotWait = options.Value.SlotWait;
        this.timeProvider = timeProvider;
        this.metrics = metrics;
        metrics.ObserveSyncWaiting(() => Volatile.Read(ref waiting));
    }

    /// <summary>
    /// Runs a visitor's work in a slot, unless too many wait or none frees in time.
    /// </summary>
    /// <exception cref="OnDemandRefusedException">No slot, for now.</exception>
    public async Task RunAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!slots.Wait(0, cancellationToken))
        {
            await WaitInLineAsync(cancellationToken).ConfigureAwait(false);
        }

        await RunHoldingAsync(work, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs the background worker's work in a slot, however long it waits.</summary>
    public async Task RunQueuedAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        Interlocked.Increment(ref waiting);
        try
        {
            await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref waiting);
        }

        await RunHoldingAsync(work, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => slots.Dispose();

    // No slot is free: takes one behind at most MaxWaitingIngestions others, within SlotWait.
    private async Task WaitInLineAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref waiting) > maxWaiting)
        {
            Interlocked.Decrement(ref waiting);
            throw Refuse(BacklogReason);
        }

        try
        {
            if (!await WaitAsync(cancellationToken).ConfigureAwait(false))
            {
                throw Refuse(SlotWaitReason);
            }
        }
        finally
        {
            Interlocked.Decrement(ref waiting);
        }
    }

    // Whether a slot was taken before the wait ran out.
    private async Task<bool> WaitAsync(CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(slotWait, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            deadline.Token,
            cancellationToken);
        try
        {
            await slots.WaitAsync(linked.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    // Called once the slot is taken: it is given back whatever the work does.
    private async Task RunHoldingAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        try
        {
            await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            slots.Release();
        }
    }

    private OnDemandRefusedException Refuse(string reason)
    {
        metrics.RecordRefusal(reason);
        return new OnDemandRefusedException(reason);
    }
}
