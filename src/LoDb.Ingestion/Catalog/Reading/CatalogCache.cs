using System.Diagnostics.CodeAnalysis;
using LoDb.Ingestion.Catalog.Snapshots;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>
/// The catalogs held in memory (<see cref="HeldCatalogs"/>) and the loads in flight, each
/// admitted by the <see cref="ColdLoadGate"/>.
/// </summary>
/// <remarks>
/// Readers of a catalog being loaded wait for the same load, which runs detached from them:
/// a reader giving up does not cancel what others wait for, and a reader joining a load
/// consumes nothing of the gate. Only a built catalog is kept: a failure, a dataset missing
/// from the store or a refusal of the gate is forgotten with its load, and the next read
/// tries again.
/// </remarks>
internal sealed class CatalogCache : IDisposable
{
    private readonly Lock gate = new();
    private readonly HeldCatalogs held;
    private readonly Dictionary<CatalogKey, Task<CacheRead>> loads = [];
    private readonly CancellationTokenSource stopping = new();
    private readonly ColdLoadGate coldGate;
    private readonly CatalogMetrics metrics;
    private readonly TimeProvider timeProvider;
    private int disposed;

    public CatalogCache(
        IOptions<CatalogOptions> options,
        ColdLoadGate coldGate,
        CatalogMetrics metrics,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        held = new HeldCatalogs(options.Value.MaxEntries, metrics.RecordEviction);
        this.coldGate = coldGate;
        this.metrics = metrics;
        this.timeProvider = timeProvider;
    }

    /// <summary>The keys of the evictable tier, the most recently read first.</summary>
    public IReadOnlyList<CatalogKey> Keys
    {
        get
        {
            lock (gate)
            {
                return held.RecentKeys;
            }
        }
    }

    /// <summary>The keys of the pinned catalogs held.</summary>
    public IReadOnlyCollection<CatalogKey> PinnedKeys
    {
        get
        {
            lock (gate)
            {
                return held.PinnedKeys;
            }
        }
    }

    /// <summary>The catalog held under the key, which becomes the most recently read.</summary>
    public bool TryGet(CatalogKey key, [NotNullWhen(true)] out CatalogSnapshot? catalog)
    {
        lock (gate)
        {
            if (!held.TryGet(key, out catalog))
            {
                return false;
            }
        }

        metrics.RecordLookup(CatalogMetrics.HitOutcome);
        return true;
    }

    /// <summary>
    /// Pins <paramref name="keys"/>: held now or once loaded, they are never evicted; the
    /// catalogs pinned before and left out become evictable.
    /// </summary>
    public void Pin(IReadOnlyCollection<CatalogKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (gate)
        {
            held.Pin(keys);
        }
    }

    /// <summary>
    /// The catalog held under the key, else the one <paramref name="load"/> builds once the
    /// gate admits it, shared with every reader of the key meanwhile.
    /// </summary>
    /// <param name="key">The (version, language).</param>
    /// <param name="load">Builds the catalog, and tells the gate who asks.</param>
    /// <param name="cancellationToken">Stops the wait, not the load.</param>
    public Task<CacheRead> GetOrLoadAsync(
        CatalogKey key,
        ColdLoad load,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(load);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) == 1, this);
        var (wait, flight) = Claim(key);
        if (flight is not null)
        {
            _ = FlyAsync(key, flight, load);
        }

        return wait.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        // The container may dispose the instance once per service it is exposed as.
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        stopping.Cancel();
        stopping.Dispose();
    }

    // The held catalog, the load in flight, or a new flight that the caller runs.
    private (Task<CacheRead> Wait, TaskCompletionSource<CacheRead>? Flight) Claim(CatalogKey key)
    {
        lock (gate)
        {
            if (held.TryGet(key, out var catalog))
            {
                metrics.RecordLookup(CatalogMetrics.HitOutcome);
                return (Task.FromResult(CacheRead.Of(catalog)), null);
            }

            if (loads.TryGetValue(key, out var running))
            {
                metrics.RecordLookup(CatalogMetrics.JoinedOutcome);
                return (running, null);
            }

            metrics.RecordLookup(CatalogMetrics.MissOutcome);
            var flight = new TaskCompletionSource<CacheRead>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            loads[key] = flight.Task;
            return (flight.Task, flight);
        }
    }

    // Never throws: the outcome goes to the flight's task.
    private async Task FlyAsync(
        CatalogKey key,
        TaskCompletionSource<CacheRead> flight,
        ColdLoad load)
    {
        CacheRead? read = null;
        Exception? failure = null;
        try
        {
            using var turn = await coldGate.EnterAsync(load.Caller, stopping.Token)
                .ConfigureAwait(false);
            read = turn is null
                ? CacheRead.Refused
                : await LoadAsync(load.Build).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        // Settled before completing: a reader woken by a failure loads again.
        lock (gate)
        {
            loads.Remove(key);
            if (read is { IsHeld: true })
            {
                held.Hold(key, read.Catalog);
            }
        }

        Complete(flight, read, failure);
    }

    private async Task<CacheRead> LoadAsync(Func<CancellationToken, Task<CatalogSnapshot?>> build)
    {
        var started = timeProvider.GetTimestamp();
        metrics.LoadStarted();
        try
        {
            var token = stopping.Token;
            var catalog = await Task.Run(() => build(token), token).ConfigureAwait(false);
            metrics.RecordLoad(
                catalog is null ? CatalogMetrics.MissingOutcome : CatalogMetrics.BuiltOutcome,
                timeProvider.GetElapsedTime(started));
            return catalog is null ? CacheRead.Missing : CacheRead.Of(catalog);
        }
        catch
        {
            metrics.RecordLoad(CatalogMetrics.FailedOutcome, timeProvider.GetElapsedTime(started));
            throw;
        }
        finally
        {
            metrics.LoadEnded();
        }
    }

    private void Complete(
        TaskCompletionSource<CacheRead> flight,
        CacheRead? read,
        Exception? failure)
    {
        if (failure is OperationCanceledException && stopping.IsCancellationRequested)
        {
            flight.SetCanceled();
        }
        else if (failure is not null)
        {
            flight.SetException(failure);
        }
        else
        {
            flight.SetResult(read ?? CacheRead.Missing);
        }
    }
}
