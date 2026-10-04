using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.WarmUp;
using LoDb.Api.Modules.Catalog.WarmUp.Progress;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Catalog.WarmUp;

/// <summary>
/// While the datasets of a cold version take their time, the stream repeats its last frame
/// every heartbeat period, so that the client's watchdog keeps waiting.
/// </summary>
public sealed class WarmUpHeartbeatTests
{
    [Fact]
    public async Task SilenceRepeatsTheLastFrameUntilTheCatalogAnswers()
    {
        var token = TestContext.Current.CancellationToken;
        var reader = new StalledCatalogReader();
        var time = new FakeTimeProvider();
        // Neither the image resolver nor the ingestion is reached: the catalog never opens.
        var gateway = new CatalogGateway(reader, null!, NullLogger<CatalogGateway>.Instance);
        var warmUp = new CatalogWarmUp(gateway, null!, time);
        var scope = new WarmUpScope(new CatalogScope("16.18.1", "en_US"), [ResourceType.Items]);

        await using var frames = warmUp.StreamAsync(scope, token).GetAsyncEnumerator(token);
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(WarmUpStage.Preparing, frames.Current.Stage);
        var heartbeat = frames.MoveNextAsync();
        Assert.False(heartbeat.IsCompleted);
        time.Advance(CatalogWarmUp.HeartbeatPeriod);
        Assert.True(await heartbeat);
        Assert.Equal(WarmUpStage.Preparing, frames.Current.Stage);

        reader.Fail(new EgressTransientException("Data Dragon did not answer."));
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(
            (WarmUpStage.Failed, "upstream-unavailable"),
            (frames.Current.Stage, frames.Current.Failure));
        Assert.False(await frames.MoveNextAsync());
    }

    // A cold catalog whose synchronous ingestion is still running.
    private sealed class StalledCatalogReader : ICatalogReader
    {
        private readonly TaskCompletionSource<CatalogLoad> load =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Fail(Exception exception) => load.SetException(exception);

        public Task<CatalogLoad> GetAsync(
            PatchVersion version,
            DdragonLanguage language,
            ColdDemand demand,
            CancellationToken cancellationToken) => load.Task.WaitAsync(cancellationToken);

        public Task<CatalogVersions> GetVersionsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PatchVersion?> GetLatestAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
