using LoDb.Domain.Versions;
using LoDb.Infrastructure.Locks;
using LoDb.Ingestion.Catalog.States.Promotion;
using LoDb.Ingestion.Catalog.States.Updates;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Ingestion.Tests.Catalog.States.Building;

/// <summary>
/// Another writer keeps <c>catalog:states</c> for longer than an update waits: the update
/// writes nothing and says so, whatever asked for it, and a promotion that finds the lock taken
/// does not fail.
/// </summary>
public sealed class LockedUpdateTests(PostgresContainerFixture postgres)
{
    private const string Version = "90.1.1";
    private const string Locked = "states.update.locked";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(StateIndexTrigger.Build)]
    [InlineData(StateIndexTrigger.Promotion)]
    [InlineData(StateIndexTrigger.Reconcile)]
    public async Task UpdateGivesUpWhileAnotherWriterHoldsTheLock(StateIndexTrigger trigger)
    {
        await using var index = await PromotedAsync();
        var writer = index.StartInstance(Hasty);
        await using var held = await HoldAsync(index);

        var report = await writer.Get<IStateIndexUpdater>()
            .UpdateAsync(new StateIndexRequest { Trigger = trigger }, Token);

        Assert.Equal(
            (StateIndexOutcome.Locked, (long?)null),
            (report.Outcome, report.GenerationId));
        Assert.Empty(await index.GenerationsAsync());
        Assert.Single(writer.Logs, static log => log.Id.Name == Locked);
    }

    [Fact]
    public async Task PromotionFindingTheLockTakenDoesNotFail()
    {
        await using var index = await PromotedAsync();
        var writer = index.StartInstance(Hasty);
        await using var held = await HoldAsync(index);
        var listener = writer.Get<IEnumerable<IPromotionListener>>()
            .OfType<StateIndexPromotionListener>()
            .Single();

        await listener.OnPromotedAsync(PatchVersion.Parse(Version), Token);

        Assert.Empty(await index.GenerationsAsync());
    }

    private async Task<StateIndexHarness> PromotedAsync()
    {
        var index = await StateIndexHarness.CreateAsync(postgres, Version);
        await index.StoreAsync(Version, await ReducedDatasets.ReadAsync());
        await index.PromoteAsync(Version);
        return index;
    }

    // The lock as another process holds it, until disposed.
    private static async Task<IAsyncDisposable> HoldAsync(StateIndexHarness index) =>
        await index.Instance.Get<IDistributedLock>().TryAcquireAsync(StateIndexLock.Name, Token)
        ?? throw new InvalidOperationException("The lock of the index is already held.");

    // The writer spends its wait for the lock at the first refusal, whatever the wait.
    private static void Hasty(IServiceCollection services) =>
        services.AddSingleton(static provider => new StateIndexLock(
            provider.GetRequiredService<IDistributedLock>(),
            new HastyClock()));

    // Each reading ten minutes after the last one: longer than any wait for the lock.
    private sealed class HastyClock : TimeProvider
    {
        private const long StepSeconds = 600;

        private long timestamp;

        public override long GetTimestamp() =>
            Interlocked.Add(ref timestamp, TimestampFrequency * StepSeconds);
    }
}
