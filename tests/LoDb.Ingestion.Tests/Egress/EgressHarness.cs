using LoDb.Ingestion.Egress;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace LoDb.Ingestion.Tests.Egress;

/// <summary>
/// The egress zone as Program.cs registers it, the primary handler swapped for a fake.
/// </summary>
internal sealed class EgressHarness : IDisposable
{
    public const string DdragonUrl = "https://ddragon.leagueoflegends.com/api/versions.json";

    private readonly ServiceProvider provider;

    private EgressHarness(ServiceProvider provider)
    {
        this.provider = provider;
    }

    public IEgressFetcher Fetcher => provider.GetRequiredService<IEgressFetcher>();

    /// <summary>
    /// The lines a real provider writes: the fake collector also keeps LogLevel.None, which
    /// every provider drops.
    /// </summary>
    public IReadOnlyList<FakeLogRecord> Lines =>
        [
            .. provider.GetFakeLogCollector().GetSnapshot()
                .Where(static record => record.Level != LogLevel.None),
        ];

    public IServiceProvider Services => provider;

    public static EgressHarness Create(
        HttpMessageHandler upstream,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(FastRetries())
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();
        var services = new ServiceCollection();
        services.AddFakeLogging();
        services.AddLoDbEgress(configuration);
        services.AddHttpClient(EgressRegistration.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => upstream);
        return new EgressHarness(services.BuildServiceProvider());
    }

    public Task<FetchOutcome> FetchAsync(string url) =>
        Fetcher.FetchAsync(
            new Uri(url, UriKind.RelativeOrAbsolute), TestContext.Current.CancellationToken);

    public void Dispose() => provider.Dispose();

    // The production backoff starts at one second: tests only need the attempts to happen.
    private static Dictionary<string, string?> FastRetries() =>
        new() { ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001" };
}
