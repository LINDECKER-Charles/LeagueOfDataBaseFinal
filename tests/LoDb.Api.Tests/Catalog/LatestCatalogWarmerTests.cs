using LoDb.Api.Workers.Catalog;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The worker pinning the latest catalogs: it pins at startup, then again every period, a
/// failed pass logged and retried at the next one.
/// </summary>
public sealed class LatestCatalogWarmerTests
{
    private const string FailedEvent = "catalog.pinning.failed";

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PinsAtStartupThenEveryPeriodPastAFailure()
    {
        using var pinning = new FlakyPinning();
        var time = new FakeTimeProvider();
        var options = new CatalogOptions();
        var logger = new FakeLogger<LatestCatalogWarmer>();
        using var warmer = new LatestCatalogWarmer(pinning, Options.Create(options), time, logger);

        await warmer.StartAsync(Token);
        await pinning.CalledAsync(1);
        time.Advance(options.VersionsLifetime);
        await pinning.CalledAsync(2);
        await warmer.StopAsync(Token);

        var failure = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal((LogLevel.Warning, FailedEvent), (failure.Level, failure.Id.Name));
    }

    // Fails its first call, as with the database out of reach, then succeeds.
    private sealed class FlakyPinning : ICatalogPinning, IDisposable
    {
        private readonly SemaphoreSlim calls = new(0);
        private int count;

        public Task PinLatestAsync(CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref count);
            calls.Release();
            return call == 1
                ? Task.FromException(new InvalidOperationException("Database out of reach."))
                : Task.CompletedTask;
        }

        public async Task CalledAsync(int expected)
        {
            while (Volatile.Read(ref count) < expected)
            {
                Assert.True(
                    await calls.WaitAsync(Deadline, Token),
                    $"Pinning not called {expected} times.");
            }
        }

        public void Dispose() => calls.Dispose();
    }
}
