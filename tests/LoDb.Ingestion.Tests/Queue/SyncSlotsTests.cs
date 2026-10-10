using System.Diagnostics.Metrics;
using LoDb.Ingestion.Pipeline;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Tests.Queue;

/// <summary>
/// The slots of the on-demand ingestions: a visitor's work waits behind a bounded number of
/// others and for a bounded time, then is refused; the worker's waits as long as it takes;
/// the waiting ones are published.
/// </summary>
public sealed class SyncSlotsTests : IDisposable
{
    private static readonly TimeSpan ShortWait = TimeSpan.FromMilliseconds(50);

    private readonly ServiceProvider provider = new ServiceCollection()
        .AddMetrics()
        .BuildServiceProvider();

    private readonly TaskCompletionSource release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WorkBeyondTheBacklogIsRefusedAtOnce()
    {
        using var slots = Create(maxWaiting: 1, TimeSpan.FromMinutes(1));
        var holding = slots.RunAsync(_ => release.Task, Token);
        var waiting = slots.RunAsync(static _ => Task.CompletedTask, Token);

        var refused = await Assert.ThrowsAsync<OnDemandRefusedException>(
            () => slots.RunAsync(static _ => Task.CompletedTask, Token));

        Assert.Equal(SyncSlots.BacklogReason, refused.Reason);
        Assert.False(waiting.IsCompleted);
        release.SetResult();
        await Task.WhenAll(holding, waiting);
    }

    [Fact]
    public async Task SlotThatDoesNotFreeInTimeIsRefused()
    {
        using var slots = Create(maxWaiting: 4, ShortWait);
        var holding = slots.RunAsync(_ => release.Task, Token);

        var refused = await Assert.ThrowsAsync<OnDemandRefusedException>(
            () => slots.RunAsync(static _ => Task.CompletedTask, Token));

        Assert.Equal(SyncSlots.SlotWaitReason, refused.Reason);
        release.SetResult();
        await holding;
        await slots.RunAsync(static _ => Task.CompletedTask, Token);
    }

    [Fact]
    public async Task WorkerWaitsAsLongAsItTakes()
    {
        using var slots = Create(maxWaiting: 0, ShortWait);
        using var waiting = new MetricCollector<int>(
            provider.GetRequiredService<IMeterFactory>(),
            IngestionMetrics.MeterName,
            "lodb.ingestion.sync.waiting");
        var holding = slots.RunAsync(_ => release.Task, Token);

        var queued = slots.RunQueuedAsync(static _ => Task.CompletedTask, Token);
        await Task.Delay(ShortWait * 2, Token);
        waiting.RecordObservableInstruments();

        Assert.False(queued.IsCompleted);
        Assert.Equal(1, waiting.LastMeasurement?.Value);
        release.SetResult();
        await Task.WhenAll(holding, queued);
    }

    public void Dispose() => provider.Dispose();

    private SyncSlots Create(int maxWaiting, TimeSpan slotWait) =>
        new(
            Options.Create(new IngestionOptions
            {
                SyncConcurrency = 1,
                MaxWaitingIngestions = maxWaiting,
                SlotWait = slotWait,
            }),
            TimeProvider.System,
            new IngestionMetrics(provider.GetRequiredService<IMeterFactory>()));
}
