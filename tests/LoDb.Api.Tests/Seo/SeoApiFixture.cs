using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Seo.IndexNow;
using LoDb.Api.Tests.Seo.States;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using LoDb.Testing.States;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The API over the recorded Data Dragon and its own database holding 16.19.1, ingested whole
/// and promoted, reading the index of states of <see cref="SeoStates"/> from memory. The
/// canonical origin is the test client's, and the promotion of 16.19.1 submitted its pages to
/// <see cref="IndexNow"/> under <see cref="IndexNowKey"/>.
/// </summary>
public sealed class SeoApiFixture(PostgresContainerFixture postgres) : IAsyncLifetime
{
    /// <summary>The origin of the test client, set as the canonical one.</summary>
    public const string Origin = "http://localhost";

    /// <summary>The IndexNow key the site publishes.</summary>
    public const string IndexNowKey = "5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e";

    private TestDatabase? _database;
    private ApiFactory? _factory;
    private HttpClient? _client;
    private IServiceProvider? _services;

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>What the IndexNow endpoint received.</summary>
    public FakeIndexNow IndexNow { get; } = new();

    /// <summary>
    /// The index of states the API reads, set before the promotion: a test may make it
    /// unavailable, and must restore it.
    /// </summary>
    public InMemoryEntityStateIndex States { get; } = new() { Snapshot = SeoStates.Snapshot() };

    /// <summary>The services of the API, to build what a test runs outside a request.</summary>
    public IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("The fixture is not initialized.");

    private HttpClient Client =>
        _client ?? throw new InvalidOperationException("The fixture is not initialized.");

    public async ValueTask InitializeAsync()
    {
        _database = await postgres.CreateMigratedDatabaseAsync(Token);
        _factory = new ApiFactory
        {
            PostgresConnectionString = _database.AppConnectionString,
            Settings = new Dictionary<string, string?>
            {
                // The production backoff starts at one second: only the attempts matter.
                ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001",
                ["LoDb:Seo:CanonicalOrigin"] = Origin,
                ["LoDb:Seo:IndexNowKey"] = IndexNowKey,
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services =>
            {
                services.AddDdragonFixtureReplay(replay);
                services.AddSingleton<IEntityStateIndex>(States);
                services.AddHttpClient(IndexNowClient.ClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => IndexNow);
            }));
        _services = host.Services;
        await IngestAsync(host.Services);

        // The redirects are what the tests check: never follow them.
        _client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    /// <summary>A GET of a path of the site.</summary>
    public Task<HttpResponseMessage> GetAsync(string path) =>
        Client.GetAsync(new Uri(path, UriKind.Relative), Token);

    /// <summary>The body of a 200 answer.</summary>
    public async Task<string> GetTextAsync(string path)
    {
        using var response = await GetAsync(path);
        var body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {response.StatusCode}");
        return body;
    }

    /// <summary>The body of a 200 JSON answer of the API.</summary>
    public async Task<JsonElement> GetJsonAsync(string path) =>
        JsonDocument.Parse(await GetTextAsync(path)).RootElement.Clone();

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    private static async Task IngestAsync(IServiceProvider services)
    {
        var ingestion = services.GetRequiredService<IVersionIngestion>();
        var latest = await ingestion.IngestAsync(
            DdragonFixtures.Latest,
            IngestionRequest.Complete,
            Token);
        Assert.Equal(VersionIngestionOutcome.Completed, latest.Outcome);
    }
}
