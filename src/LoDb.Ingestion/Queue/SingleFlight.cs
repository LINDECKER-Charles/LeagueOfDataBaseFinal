namespace LoDb.Ingestion.Queue;

/// <summary>
/// Merges identical work: a key already in flight is waited for, never run twice at once.
/// </summary>
/// <remarks>
/// The work runs detached from its callers, on the service's own token: a caller giving up
/// stops waiting without cancelling what others wait for. A failure reaches every caller of
/// the flight and is forgotten with it, so the next call runs the work again.
/// </remarks>
internal sealed class SingleFlight : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, Task> flights = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource stopping = new();
    private int disposed;

    /// <summary>
    /// Runs <paramref name="work"/> for the keys no flight covers, then waits for every key.
    /// </summary>
    /// <param name="keys">The keys needed by the caller.</param>
    /// <param name="work">The work, given the keys it is to cover.</param>
    /// <param name="cancellationToken">Stops the wait, not the work.</param>
    public Task RunAsync(
        IReadOnlyCollection<string> keys,
        Func<IReadOnlyList<string>, CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(work);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) == 1, this);
        var flight = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (claimed, waits) = Claim(keys, flight.Task);
        if (claimed.Count > 0)
        {
            _ = FlyAsync(new Flight(flight, claimed, work));
            waits.Add(flight.Task);
        }

        return Task.WhenAll(waits).WaitAsync(cancellationToken);
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

    // Each key joins the flight already running it, or is claimed by the new one.
    private (List<string> Claimed, List<Task> Waits) Claim(
        IReadOnlyCollection<string> keys,
        Task flight)
    {
        List<string> claimed = [];
        List<Task> waits = [];
        lock (gate)
        {
            foreach (var key in keys.Distinct(StringComparer.Ordinal))
            {
                if (flights.TryGetValue(key, out var running))
                {
                    waits.Add(running);
                }
                else
                {
                    flights[key] = flight;
                    claimed.Add(key);
                }
            }
        }

        return (claimed, waits);
    }

    // Never throws: the outcome goes to the flight's task.
    private async Task FlyAsync(Flight flight)
    {
        Exception? failure = null;
        try
        {
            var token = stopping.Token;
            await Task.Run(() => flight.Work(flight.Keys, token), token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        // Forgotten before completing: a caller woken by the outcome asks for new work.
        lock (gate)
        {
            foreach (var key in flight.Keys)
            {
                flights.Remove(key);
            }
        }

        Complete(flight.Source, failure);
    }

    private void Complete(TaskCompletionSource source, Exception? failure)
    {
        if (failure is OperationCanceledException && stopping.IsCancellationRequested)
        {
            source.SetCanceled();
        }
        else if (failure is not null)
        {
            source.SetException(failure);
        }
        else
        {
            source.SetResult();
        }
    }

    private sealed record Flight(
        TaskCompletionSource Source,
        IReadOnlyList<string> Keys,
        Func<IReadOnlyList<string>, CancellationToken, Task> Work);
}
