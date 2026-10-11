using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LoDb.Api.Tests.Catalog.WarmUp;

/// <summary>
/// An API host of its own over the recorded Data Dragon, every version cold, whose budgets of
/// cold work a test sets: the catalog tests' shared host keeps the production ones.
/// </summary>
internal sealed class WarmUpBudgetApi : IAsyncDisposable
{
    private const string IngestionSection = "LoDb:Ingestion";
    private const string IngestionMeter = "LoDb.Ingestion";
    private const string RefusalCounter = "lodb.ingestion.on_demand.refused";

    private readonly TestDatabase database;
    private readonly ApiFactory factory;
    private readonly FixtureReplayHandler replay;
    private readonly HttpClient client;

    private WarmUpBudgetApi(TestDatabase database, ApiFactory factory, FixtureReplayHandler replay)
    {
        this.database = database;
        this.factory = factory;
        this.replay = replay;
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddDdragonFixtureReplay(replay)));
        client = host.CreateClient();
        Refusals = new MetricCollector<long>(
            host.Services.GetRequiredService<IMeterFactory>(),
            IngestionMeter,
            RefusalCounter);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The refusals of on-demand work, counted since the host started.</summary>
    public MetricCollector<long> Refusals { get; }

    /// <summary>What reached the recorded Data Dragon.</summary>
    public IReadOnlyList<Uri> UpstreamRequests => replay.Requests;

    /// <summary>A host with one <c>LoDb:Ingestion</c> budget set, the others as served.</summary>
    public static async Task<WarmUpBudgetApi> StartAsync(
        PostgresContainerFixture postgres,
        (string Key, int PerMinute) budget)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        var database = await postgres.CreateDatabaseAsync(Token);
        await database.MigrateAsync(Token);
        var factory = new ApiFactory
        {
            PostgresConnectionString = database.AppConnectionString,
            Settings = new Dictionary<string, string?>
            {
                // The production backoff starts at one second: only the attempts matter.
                ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001",
                [$"{IngestionSection}:{budget.Key}"] =
                    budget.PerMinute.ToString(CultureInfo.InvariantCulture),
            },
        };
        return new WarmUpBudgetApi(database, factory, DdragonFixtures.CreateReplay());
    }

    /// <summary>The frames of a warm-up stream, read to its end.</summary>
    public async Task<IReadOnlyList<JsonElement>> WarmUpAsync(string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var frames = SseFrames.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.NotEmpty(frames);
        return frames;
    }

    public Task<HttpResponseMessage> GetAsync(string path) =>
        client.GetAsync(new Uri(path, UriKind.Relative), Token);

    public async ValueTask DisposeAsync()
    {
        Refusals.Dispose();
        client.Dispose();
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }
}
