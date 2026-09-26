using System.Collections.Concurrent;
using LoDb.Ingestion.Queue;

namespace LoDb.Ingestion.Tests.Queue;

/// <summary>
/// Identical work in flight runs once: its callers share the outcome, a caller giving up
/// leaves it running, and a failure is forgotten with its flight.
/// </summary>
public sealed class SingleFlightTests : IDisposable
{
    private readonly SingleFlight flights = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CallersOfAKeyInFlightShareOneRun()
    {
        var gate = NewGate();
        var runs = 0;
        Task Work(IReadOnlyList<string> keys, CancellationToken token)
        {
            Interlocked.Increment(ref runs);
            return gate.Task;
        }

        var first = flights.RunAsync(["a"], Work, Token);
        var second = flights.RunAsync(["a"], Work, Token);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task CallerRunsOnlyTheKeysNoFlightCovers()
    {
        var gate = NewGate();
        var covered = new ConcurrentQueue<string>();
        Task Work(IReadOnlyList<string> keys, CancellationToken token)
        {
            covered.Enqueue(string.Join(',', keys));
            return gate.Task;
        }

        var first = flights.RunAsync(["a"], Work, Token);
        var second = flights.RunAsync(["a", "b", "b", "c"], Work, Token);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(["a", "b,c"], covered.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task FailureReachesEveryCallerThenIsForgotten()
    {
        var gate = NewGate();
        var runs = 0;
        async Task Work(IReadOnlyList<string> keys, CancellationToken token)
        {
            if (Interlocked.Increment(ref runs) == 1)
            {
                await gate.Task;
                throw new InvalidOperationException("The first run fails.");
            }
        }

        var first = flights.RunAsync(["a"], Work, Token);
        var second = flights.RunAsync(["a"], Work, Token);
        gate.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second);
        await flights.RunAsync(["a"], Work, Token);
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task CallerGivingUpLeavesTheWorkRunning()
    {
        var gate = NewGate();
        var workToken = new CancellationToken(canceled: true);
        async Task Work(IReadOnlyList<string> keys, CancellationToken token)
        {
            workToken = token;
            await gate.Task;
        }

        using var impatient = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var leaving = flights.RunAsync(["a"], Work, impatient.Token);
        var staying = flights.RunAsync(["a"], Work, Token);
        await impatient.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leaving);
        Assert.False(staying.IsCompleted);
        gate.SetResult();
        await staying;
        Assert.False(workToken.IsCancellationRequested);
    }

    [Fact]
    public async Task DisposeCancelsTheWorkInFlight()
    {
        var started = NewGate();
        async Task Work(IReadOnlyList<string> keys, CancellationToken token)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }

        var waiting = flights.RunAsync(["a"], Work, Token);
        await started.Task.WaitAsync(Token);
        flights.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = flights.RunAsync(["b"], Work, Token);
        });
    }

    public void Dispose() => flights.Dispose();

    private static TaskCompletionSource NewGate() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
