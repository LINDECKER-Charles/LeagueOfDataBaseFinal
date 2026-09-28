using System.Net;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// The API over the recorded Data Dragon and a database holding 16.19.1, ingested whole and
/// promoted: the latest version. The other recorded versions stay cold until a redirect
/// looks a name up in them.
/// </summary>
/// <remarks>
/// The client never follows a redirect: the 301 itself is what the tests read.
/// </remarks>
public sealed class LegacyApiFixture : IAsyncLifetime
{
    /// <summary>Prefix nginx writes before an old path.</summary>
    public const string Prefix = "/api/legacy";

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
                ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001",
                // The promotion of the ingestion below must be seen by the first redirect.
                ["LoDb:Catalog:VersionsLifetime"] = "00:00:00.001",
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddDdragonFixtureReplay(replay)));
        await IngestAsync(host.Services.GetRequiredService<IVersionIngestion>());
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

        await _postgres.DisposeAsync();
    }

    private static async Task IngestAsync(IVersionIngestion ingestion)
    {
        PatchVersion latest = DdragonFixtures.Latest;
        var result = await ingestion.IngestAsync(latest, IngestionRequest.Complete, Token);
        Assert.Equal(VersionIngestionOutcome.Completed, result.Outcome);
    }
}
