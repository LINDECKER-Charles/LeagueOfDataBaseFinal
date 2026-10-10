using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>
/// Admits the loads of catalogs no memory holds: a few at a time and at a bounded rate,
/// whatever the number of keys a client sweeps (<c>LoDb:Catalog:ColdLoad*</c>).
/// </summary>
/// <remarks>
/// <para>
/// A load is CPU-bound for a tenth of a second or more: bounding how many run at once bounds
/// the cores, bounding the rate bounds the time they stay busy. The rate is split between two
/// partitions. The renderer's (<see cref="ColdCaller.Internal"/>) may wait a short while for
/// its turn; the public one never waits, is refused at once, and may hold every slot but one,
/// so that a page always finds room.
/// </para>
/// <para>
/// Only a true miss comes here: a reader joining a load in flight consumes nothing.
/// </para>
/// </remarks>
internal sealed class ColdLoadGate : IDisposable
{
    private static readonly TimeSpan RatePeriod = TimeSpan.FromSeconds(1);

    private readonly ConcurrencyLimiter loads;
    private readonly ConcurrencyLimiter publicLoads;
    private readonly TokenBucketRateLimiter internalRate;
    private readonly TokenBucketRateLimiter publicRate;
    private readonly TimeSpan internalWait;
    private readonly TimeProvider timeProvider;
    private readonly CatalogMetrics metrics;

    public ColdLoadGate(
        IOptions<CatalogOptions> options,
        TimeProvider timeProvider,
        CatalogMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value;
        var internalPerSecond = settings.ColdLoadsPerSecond - settings.PublicColdLoadsPerSecond;
        // In 64 bits: the product of two large settings overflows an int, and the quotient,
        // at most the burst, fits back.
        var internalBurst = (int)Math.Max(
            1L,
            (long)settings.ColdLoadBurst * internalPerSecond / settings.ColdLoadsPerSecond);
        loads = Concurrency(settings.ColdLoadConcurrency, settings.MaxPendingLoads);
        publicLoads = Concurrency(Math.Max(1, settings.ColdLoadConcurrency - 1), 0);
        internalRate = Rate(internalPerSecond, internalBurst, settings.MaxPendingLoads);
        publicRate = Rate(
            settings.PublicColdLoadsPerSecond,
            Math.Max(1, settings.ColdLoadBurst - internalBurst),
            0);
        internalWait = settings.InternalColdLoadWait;
        this.timeProvider = timeProvider;
        this.metrics = metrics;
    }

    /// <summary>The turn of a load, to dispose once it ends; null when refused.</summary>
    /// <param name="caller">The partition the load is counted in.</param>
    /// <param name="cancellationToken">Stops an internal wait: the instance stops.</param>
    public async Task<IDisposable?> EnterAsync(
        ColdCaller caller,
        CancellationToken cancellationToken)
    {
        var turn = caller == ColdCaller.Internal
            ? await EnterInternalAsync(cancellationToken).ConfigureAwait(false)
            : EnterPublic();
        metrics.RecordAdmission(caller, turn is not null);
        return turn;
    }

    public void Dispose()
    {
        loads.Dispose();
        publicLoads.Dispose();
        internalRate.Dispose();
        publicRate.Dispose();
    }

    private static ConcurrencyLimiter Concurrency(int permits, int queued) => new(
        new ConcurrencyLimiterOptions
        {
            PermitLimit = permits,
            QueueLimit = queued,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });

    private static TokenBucketRateLimiter Rate(int perSecond, int burst, int queued) => new(
        new TokenBucketRateLimiterOptions
        {
            TokenLimit = burst,
            TokensPerPeriod = perSecond,
            ReplenishmentPeriod = RatePeriod,
            QueueLimit = queued,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });

    // A token spent on a load refused for its slot stays spent: the rate only gets stricter.
    private Turn? EnterPublic()
    {
        using var token = publicRate.AttemptAcquire();
        if (!token.IsAcquired)
        {
            return null;
        }

        var publicSlot = publicLoads.AttemptAcquire();
        if (!publicSlot.IsAcquired)
        {
            publicSlot.Dispose();
            return null;
        }

        var slot = loads.AttemptAcquire();
        if (!slot.IsAcquired)
        {
            slot.Dispose();
            publicSlot.Dispose();
            return null;
        }

        return new Turn(slot, publicSlot);
    }

    private async Task<Turn?> EnterInternalAsync(CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(internalWait, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            deadline.Token,
            cancellationToken);
        try
        {
            using var token = await internalRate.AcquireAsync(1, linked.Token)
                .ConfigureAwait(false);
            var slot = token.IsAcquired
                ? await loads.AcquireAsync(1, linked.Token).ConfigureAwait(false)
                : null;
            if (slot is { IsAcquired: true })
            {
                return new Turn(slot, null);
            }

            slot?.Dispose();
            return null;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return null;
        }
    }

    private sealed class Turn(RateLimitLease slot, RateLimitLease? publicSlot) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 1)
            {
                return;
            }

            slot.Dispose();
            publicSlot?.Dispose();
        }
    }
}
