using System.Net;
using System.Text.Json;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The API over the recorded Data Dragon and its own database holding 16.19.1, ingested whole
/// and promoted. Crawler reads only queue the other versions, and the workers are off: their
/// sitemaps stay pending whatever the order of the tests.
/// </summary>
public sealed class SeoApiFixture : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres = new();
    private TestDatabase? _database;
    private ApiFactory? _factory;
    private HttpClient? _client;

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    private HttpClient Client =>
        _client ?? throw new InvalidOperationException("The fixture is not initialized.");

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _database = await _postgres.CreateDatabaseAsync(Token);
        await _database.MigrateAsync(Token);
        _factory = new ApiFactory
        {
            PostgresConnectionString = _database.ConnectionString,
            Settings = new Dictionary<string, string?>
            {
                // The production backoff starts at one second: only the attempts matter.
                ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001",
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddDdragonFixtureReplay(replay)));
        var ingestion = host.Services.GetRequiredService<IVersionIngestion>();
        var result = await ingestion.IngestAsync(
            DdragonFixtures.Latest,
            IngestionRequest.Complete,
            Token);
        Assert.Equal(VersionIngestionOutcome.Completed, result.Outcome);

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

        await _postgres.DisposeAsync();
    }
}
