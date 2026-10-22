using System.Net;
using System.Text.Json;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using LoDb.Testing.States;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Catalog.States;

/// <summary>
/// The API over the recorded Data Dragon and a database holding 16.19.1, ingested whole and
/// promoted, with an index of states held in memory that each test seeds, and catalogs a test
/// may hold back. 16.18.1 and older versions stay cold until a call reads them.
/// </summary>
public sealed class StateApiFixture(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private const string ProblemContentType = "application/problem+json";
    private const string UnboundedColdLoads = "1000";
    private const string PublicColdLoads = "500";

    private TestDatabase? _database;
    private ApiFactory? _factory;
    private HttpClient? _client;
    private HeldBackCatalogReader? _reader;

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The index of states the API reads.</summary>
    public InMemoryEntityStateIndex States { get; } = new();

    /// <summary>The catalog reader of the API, which holds back what a test asks.</summary>
    internal HeldBackCatalogReader Reader => _reader ?? throw NotInitialized();

    private HttpClient Client => _client ?? throw NotInitialized();

    public async ValueTask InitializeAsync()
    {
        _database = await postgres.CreateMigratedDatabaseAsync(Token);
        // The budgets of cold work are not what these tests read, and an opener reads two
        // catalogs no memory holds: at the served rates, a test would be refused the work of
        // the tests before it.
        _factory = new ApiFactory
        {
            PostgresConnectionString = _database.AppConnectionString,
            Settings = new Dictionary<string, string?>
            {
                ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001",
                ["LoDb:Catalog:VersionsLifetime"] = "00:00:00.001",
                ["LoDb:Catalog:ColdLoadsPerSecond"] = UnboundedColdLoads,
                ["LoDb:Catalog:PublicColdLoadsPerSecond"] = PublicColdLoads,
                ["LoDb:Catalog:ColdLoadBurst"] = UnboundedColdLoads,
                ["LoDb:Ingestion:ColdDatasetsPerMinute"] = UnboundedColdLoads,
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => HoldBackCatalogs(services
                .AddDdragonFixtureReplay(replay)
                .AddSingleton<IEntityStateIndex>(States))));
        var ingestion = host.Services.GetRequiredService<IVersionIngestion>();
        var result = await ingestion.IngestAsync(
            DdragonFixtures.Latest,
            IngestionRequest.Complete,
            Token);
        Assert.Equal(VersionIngestionOutcome.Completed, result.Outcome);
        _reader = host.Services.GetRequiredService<HeldBackCatalogReader>();
        _client = host.CreateClient();
    }

    /// <summary>Seeds the index with a snapshot over <paramref name="versions"/>.</summary>
    public void Seed(IEnumerable<string> versions, IEnumerable<IndexedState> states) =>
        States.Snapshot = TestStates.Snapshot(versions, states);

    /// <summary>Empties the index, makes it readable and releases every catalog.</summary>
    public void Reset()
    {
        States.Snapshot = EntityStateSnapshot.Empty;
        States.IsUnavailable = false;
        Reader.ReleaseAll();
    }

    /// <summary>
    /// A catalog read as the renderer reads it, whose loads wait for their turn where a
    /// public caller's are refused.
    /// </summary>
    public async Task<CatalogSnapshot> CatalogAsync(string version, string language)
    {
        var load = await Reader.GetAsync(
            PatchVersion.Parse(version),
            DdragonLanguage.Parse(language),
            ColdDemand.Synchronous.By(ColdCaller.Internal),
            Token);
        Assert.True(load.IsReady, $"{version}/{language}: {load.Status}");
        return load.Catalog;
    }

    public Task<HttpResponseMessage> GetAsync(string path) =>
        Client.GetAsync(new Uri(path, UriKind.Relative), Token);

    /// <summary>The body of a 200 answer.</summary>
    public async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await GetAsync(path);
        var body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>The ProblemDetails of a failed call, its status checked.</summary>
    public async Task<JsonElement> GetProblemAsync(string path, HttpStatusCode status)
    {
        using var response = await GetAsync(path);
        var body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode == status, $"{path}: {response.StatusCode} {body}");
        Assert.Equal(ProblemContentType, response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

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

    private static InvalidOperationException NotInitialized() =>
        new("The fixture is not initialized.");

    // The API's own reader, wrapped: every service that reads catalogs reads them through it.
    private static void HoldBackCatalogs(IServiceCollection services)
    {
        var original = services.Last(static service =>
            service.ServiceType == typeof(ICatalogReader));
        var type = original.ImplementationType
            ?? throw new InvalidOperationException("The catalog reader is not a type.");
        services.AddSingleton(provider => new HeldBackCatalogReader(
            (ICatalogReader)ActivatorUtilities.CreateInstance(provider, type)));
        services.AddSingleton<ICatalogReader>(static provider =>
            provider.GetRequiredService<HeldBackCatalogReader>());
    }
}
