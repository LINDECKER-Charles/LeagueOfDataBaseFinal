using System.Net;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using LoDb.Testing.States;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// The API over the recorded Data Dragon and a database holding 16.19.1, ingested whole and
/// promoted: the latest version. The other recorded versions stay cold until a redirect
/// looks a name up in them. The index of states is held in memory, empty until a test seeds
/// it.
/// </summary>
/// <remarks>
/// The client never follows a redirect: the 301 itself is what the tests read. A test that
/// seeds the index empties it before it ends: the tests of the group run one at a time.
/// </remarks>
public sealed class LegacyApiFixture(PostgresContainerFixture postgres) : IAsyncLifetime
{
    /// <summary>Prefix nginx writes before an old path.</summary>
    public const string Prefix = "/api/legacy";

    private TestDatabase? _database;
    private ApiFactory? _factory;
    private HttpClient? _client;
    private FixtureReplayHandler? _replay;
    private IServiceProvider? _services;

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The index of states the API reads.</summary>
    public InMemoryEntityStateIndex States { get; } = new();

    /// <summary>The services of the API host, such as its catalog reader.</summary>
    public IServiceProvider Services => _services ?? throw NotInitialized();

    /// <summary>What reached the recorded Data Dragon, the latest's ingestion included.</summary>
    public IReadOnlyList<Uri> UpstreamRequests =>
        _replay?.Requests ?? throw NotInitialized();

    /// <summary>The storage of the API: what a call ingested lands under it.</summary>
    public string StorageRoot =>
        _factory?.StorageRoot ?? throw NotInitialized();

    private HttpClient Client =>
        _client ?? throw NotInitialized();

    public async ValueTask InitializeAsync()
    {
        _database = await postgres.CreateMigratedDatabaseAsync(Token);
        _factory = new ApiFactory
        {
            PostgresConnectionString = _database.AppConnectionString,
            Settings = new Dictionary<string, string?>
            {
                ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001",
                // The promotion of the ingestion below must be seen by the first redirect.
                ["LoDb:Catalog:VersionsLifetime"] = "00:00:00.001",
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        _replay = replay;
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services
                .AddDdragonFixtureReplay(replay)
                .AddSingleton<IEntityStateIndex>(States)));
        await IngestAsync(host.Services.GetRequiredService<IVersionIngestion>());
        _services = host.Services;
        _client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    /// <summary>An old URL, sent as nginx forwards it.</summary>
    public async Task<HttpResponseMessage> SendAsync(string oldUrl, HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(
            method ?? HttpMethod.Get,
            new Uri(Prefix + oldUrl, UriKind.Relative));
        return await Client.SendAsync(request, Token);
    }

    /// <summary>A call of the API itself, such as a catalog read ingesting its version.</summary>
    public Task<HttpResponseMessage> GetApiAsync(string path) =>
        Client.GetAsync(new Uri(path, UriKind.Relative), Token);

    /// <summary>The target of an old URL, its 301 checked.</summary>
    public async Task<string> LocationOfAsync(string oldUrl)
    {
        using var response = await SendAsync(oldUrl);
        var body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(
            response.StatusCode == HttpStatusCode.MovedPermanently,
            $"{oldUrl}: {response.StatusCode} {body}");
        return response.Headers.Location?.OriginalString
            ?? throw new InvalidOperationException($"{oldUrl}: 301 without Location.");
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

    private static async Task IngestAsync(IVersionIngestion ingestion)
    {
        PatchVersion latest = DdragonFixtures.Latest;
        var result = await ingestion.IngestAsync(latest, IngestionRequest.Complete, Token);
        Assert.Equal(VersionIngestionOutcome.Completed, result.Outcome);
    }
}
