using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.WarmUp;
using LoDb.Api.Modules.Catalog.WarmUp.Limits;
using LoDb.Api.Modules.Catalog.WarmUp.Progress;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Catalog.WarmUp;

/// <summary>
/// While the datasets of a cold version take their time, the stream repeats its last frame
/// every heartbeat period, so that the client's watchdog keeps waiting; past its lifetime, it
/// ends with a failed frame, and the client visits the page.
/// </summary>
public sealed class WarmUpHeartbeatTests
{
    private static readonly WarmUpScope Scope =
        new(new CatalogScope("16.18.1", "en_US"), [ResourceType.Items]);

    private readonly StalledCatalogReader reader = new();
    private readonly FakeTimeProvider time = new();
    private readonly WarmUpOptions options = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SilenceRepeatsTheLastFrameUntilTheCatalogAnswers()
    {
        await using var frames = CreateWarmUp().StreamAsync(Scope, Token).GetAsyncEnumerator(Token);
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

    [Fact]
    public async Task StreamPastItsLifetimeEndsWithAPendingFailure()
    {
        await using var frames = CreateWarmUp().StreamAsync(Scope, Token).GetAsyncEnumerator(Token);
        Assert.True(await frames.MoveNextAsync());

        time.Advance(options.StreamLifetime);
        var last = frames.Current;
        while (await frames.MoveNextAsync())
        {
            last = frames.Current;
        }

        Assert.Equal((WarmUpStage.Failed, "catalog-pending"), (last.Stage, last.Failure));
    }

    // Neither the image resolver nor the ingestion is reached: the catalog never opens.
    private CatalogWarmUp CreateWarmUp() =>
        new(
            new CatalogGateway(reader, null!, NullLogger<CatalogGateway>.Instance),
            null!,
            Options.Create(options),
            time);

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
