using System.Diagnostics.Metrics;
using LoDb.Infrastructure.Jobs;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Storage;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Pipeline;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace LoDb.Ingestion.Tests.Pipeline;

/// <summary>
/// One API instance: the zones as Program.cs registers them, answered by its own replay of
/// the recording, over the database, storage and clock of an <see cref="IngestionHarness"/>.
/// </summary>
internal sealed class IngestionInstance : IAsyncDisposable
{
    private readonly ServiceProvider provider;

    public IngestionInstance(IConfiguration configuration, TimeProvider clock)
    {
        Upstream = new FlakyUpstream(DdragonFixtures.CreateReplay());
        var services = new ServiceCollection()
            .AddSingleton(configuration)
            .AddSingleton(clock)
            .AddFakeLogging()
            .AddMetrics()
            .AddLoDbEgress(configuration)
            .AddLoDbStorage(configuration)
            .AddLoDbPersistence(configuration)
            .AddLoDbJobs(configuration)
            .AddLoDbDdragon(configuration)
            .AddLoDbIngestion(configuration)
            .AddLoDbCatalog(configuration);
        services.AddHttpClient(EgressRegistration.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => Upstream);
        provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    public FlakyUpstream Upstream { get; }

    public FixtureReplayHandler Replay => Upstream.Replay;

    public IMeterFactory Meters => Get<IMeterFactory>();

    /// <summary>The lines a real provider writes, LogLevel.None aside.</summary>
    public IReadOnlyList<FakeLogRecord> Logs =>
        [
            .. provider.GetFakeLogCollector().GetSnapshot()
                .Where(static record => record.Level != LogLevel.None),
        ];

    public T Get<T>()
        where T : notnull =>
        provider.GetRequiredService<T>();

    /// <summary>Requests that reached the recording, by URL path.</summary>
    public IReadOnlyList<string> RequestedPaths(Func<Uri, bool>? filter = null) =>
        [
            .. Replay.Requests
                .Where(url => filter?.Invoke(url) ?? true)
                .Select(static url => url.AbsolutePath),
        ];

    public ValueTask DisposeAsync() => provider.DisposeAsync();
}
