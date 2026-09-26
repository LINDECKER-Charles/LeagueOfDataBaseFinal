using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Normalization;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Ingestion.Tests.Ddragon;

/// <summary>
/// The egress and Data Dragon zones as Program.cs registers them, answered by the recorded
/// fixtures instead of the network.
/// </summary>
internal sealed class ReplayHarness : IDisposable
{
    private readonly ServiceProvider provider;

    private ReplayHarness(ServiceProvider provider, FixtureReplayHandler replay)
    {
        this.provider = provider;
        Replay = replay;
    }

    public FixtureReplayHandler Replay { get; }

    public IDdragonClient Client => provider.GetRequiredService<IDdragonClient>();

    public IDdragonDatasets Datasets => provider.GetRequiredService<IDdragonDatasets>();

    public IEgressFetcher Fetcher => provider.GetRequiredService<IEgressFetcher>();

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public static ReplayHarness Create()
    {
        var replay = DdragonFixtures.CreateReplay();
        return new ReplayHarness(
            Build(services => services.AddDdragonFixtureReplay(replay)), replay);
    }

    /// <summary>The same zones over a scripted upstream, for answers no recording holds.</summary>
    public static ReplayHarness Over(HttpMessageHandler upstream) =>
        new(
            Build(services => services.AddHttpClient(EgressRegistration.ClientName)
                .ConfigurePrimaryHttpMessageHandler(() => upstream)),
            DdragonFixtures.CreateReplay());

    public static DatasetScope Scope(string version, string language) => new()
    {
        Version = PatchVersion.Parse(version),
        Language = DdragonLanguage.Parse(language),
    };

    public void Dispose() => provider.Dispose();

    private static ServiceProvider Build(Action<IServiceCollection> upstream)
    {
        // The production backoff starts at one second: tests only need the attempts to happen.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLoDbEgress(configuration);
        services.AddLoDbDdragon(configuration);
        upstream(services);
        return services.BuildServiceProvider();
    }
}
