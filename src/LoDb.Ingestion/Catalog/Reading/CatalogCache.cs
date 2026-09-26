using System.Diagnostics.CodeAnalysis;
using LoDb.Ingestion.Catalog.Snapshots;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>
/// The catalogs held in memory, the least recently read evicted first
/// (<c>LoDb:Catalog:MaxEntries</c>), and the loads in flight.
/// </summary>
/// <remarks>
/// Readers of a catalog being loaded wait for the same load, which runs detached from them:
/// a reader giving up does not cancel what others wait for. Only a built catalog is kept: a
/// failure, or a dataset missing from the store, is forgotten with its load, and the next
/// read loads again.
/// </remarks>
internal sealed class CatalogCache : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<CatalogKey, LinkedListNode<Held>> held = [];
    private readonly LinkedList<Held> recency = new();
    private readonly Dictionary<CatalogKey, Task<CatalogSnapshot?>> loads = [];
    private readonly CancellationTokenSource stopping = new();
    private readonly int capacity;
    private int disposed;

    public CatalogCache(IOptions<CatalogOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        capacity = options.Value.MaxEntries;
    }

    /// <summary>The keys held, the most recently read first.</summary>
    public IReadOnlyList<CatalogKey> Keys
    {
        get
        {
            lock (gate)
            {
                return [.. recency.Select(static entry => entry.Key)];
            }
        }
    }

    /// <summary>The catalog held under the key, which becomes the most recently read.</summary>
    public bool TryGet(CatalogKey key, [NotNullWhen(true)] out CatalogSnapshot? catalog)
    {
        lock (gate)
        {
            if (held.TryGetValue(key, out var node))
            {
                Touch(node);
                catalog = node.Value.Catalog;
                return true;
            }
        }

        catalog = null;
        return false;
    }

    /// <summary>
    /// The catalog held under the key, else the one <paramref name="load"/> builds, shared
    /// with every reader of the key meanwhile.
    /// </summary>
    /// <param name="key">The (version, language).</param>
    /// <param name="load">
    /// Builds the catalog; returns <see langword="null"/> when a dataset is missing.
    /// </param>
    /// <param name="cancellationToken">Stops the wait, not the load.</param>
    public Task<CatalogSnapshot?> GetOrLoadAsync(
        CatalogKey key,
        Func<CancellationToken, Task<CatalogSnapshot?>> load,
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
    private (Task<CatalogSnapshot?> Wait, TaskCompletionSource<CatalogSnapshot?>? Flight) Claim(
        CatalogKey key)
    {
        lock (gate)
        {
            if (held.TryGetValue(key, out var node))
            {
                Touch(node);
                return (Task.FromResult<CatalogSnapshot?>(node.Value.Catalog), null);
            }

            if (loads.TryGetValue(key, out var running))
            {
                return (running, null);
            }

            var flight = new TaskCompletionSource<CatalogSnapshot?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            loads[key] = flight.Task;
            return (flight.Task, flight);
        }
    }

    // Never throws: the outcome goes to the flight's task.
    private async Task FlyAsync(
        CatalogKey key,
        TaskCompletionSource<CatalogSnapshot?> flight,
        Func<CancellationToken, Task<CatalogSnapshot?>> load)
    {
        CatalogSnapshot? catalog = null;
        Exception? failure = null;
        try
        {
            var token = stopping.Token;
            catalog = await Task.Run(() => load(token), token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        // Settled before completing: a reader woken by a failure loads again.
        lock (gate)
        {
            loads.Remove(key);
            if (catalog is not null)
            {
                Hold(key, catalog);
            }
        }

        Complete(flight, catalog, failure);
    }

    private void Complete(
        TaskCompletionSource<CatalogSnapshot?> flight,
        CatalogSnapshot? catalog,
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
            flight.SetResult(catalog);
        }
    }

    // Under the gate.
    private void Hold(CatalogKey key, CatalogSnapshot catalog)
    {
        held[key] = recency.AddFirst(new Held(key, catalog));
        while (held.Count > capacity && recency.Last is { } oldest)
        {
            recency.RemoveLast();
            held.Remove(oldest.Value.Key);
        }
    }

    // Under the gate.
    private void Touch(LinkedListNode<Held> node)
    {
        recency.Remove(node);
        recency.AddFirst(node);
    }

    private sealed record Held(CatalogKey Key, CatalogSnapshot Catalog);
}
