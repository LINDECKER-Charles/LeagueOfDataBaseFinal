using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The API over the recorded Data Dragon and its own database holding 16.19.1, ingested whole
/// and promoted, and the en_US datasets of <see cref="Archived"/>, the sitemaps' language,
/// read once as a visitor would (its images wait for the workers, which are off).
/// Crawler reads only queue the other versions, and the workers are off: their sitemaps stay
/// pending whatever the order of the tests. The sitemap index announces
/// <see cref="SitemapVersionWindow"/> past versions, 16.18.1 only: 8.7.1 lies outside.
/// </summary>
public sealed class SeoApiFixture(PostgresContainerFixture postgres) : IAsyncLifetime
{
    /// <summary>The past versions the index announces, few enough for the fixtures.</summary>
    public const int SitemapVersionWindow = 1;

    /// <summary>A past version outside the window, its en_US datasets stored.</summary>
    public static readonly PatchVersion Archived = PatchVersion.Parse("8.7.1");

    private TestDatabase? _database;
    private ApiFactory? _factory;
    private HttpClient? _client;

    public static CancellationToken Token => TestContext.Current.CancellationToken;

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
                ["LoDb:Seo:SitemapVersionWindow"] =
                    SitemapVersionWindow.ToString(CultureInfo.InvariantCulture),
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddDdragonFixtureReplay(replay)));
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
        var archived = await services.GetRequiredService<ICatalogReader>().GetAsync(
            Archived,
            DdragonLanguage.EnUs,
            ColdDemand.Synchronous.By(ColdCaller.Internal),
            Token);
        Assert.True(archived.IsReady, $"{Archived.Value}: {archived.Status}");
    }
}
