using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The API over the recorded Data Dragon and a database holding 16.19.1, ingested whole and
/// promoted. The other recorded versions stay cold: a call ingests their datasets before it
/// answers, and their images only when it resolves them synchronously, the background
/// workers being off.
/// </summary>
public sealed class CatalogApiFixture : IAsyncLifetime
{
    private const string ProblemContentType = "application/problem+json";

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
                // Read the promoted version again on every call, whatever ran before.
                ["LoDb:Catalog:VersionsLifetime"] = "00:00:00.001",
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddDdragonFixtureReplay(replay)));
        var ingestion = host.Services.GetRequiredService<IVersionIngestion>();
        await IngestAsync(ingestion, DdragonFixtures.Latest);
        _client = host.CreateClient();
    }

    /// <summary>A GET, optionally conditional on a tag the client holds.</summary>
    public async Task<HttpResponseMessage> GetAsync(string path, string? ifNoneMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (ifNoneMatch is not null)
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(ifNoneMatch));
        }

        return await Client.SendAsync(request, Token);
    }

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

        await _postgres.DisposeAsync();
    }

    private static async Task IngestAsync(IVersionIngestion ingestion, PatchVersion version)
    {
        var result = await ingestion.IngestAsync(version, IngestionRequest.Complete, Token);
        Assert.Equal(VersionIngestionOutcome.Completed, result.Outcome);
    }
}
