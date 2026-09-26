using System.Net.Http.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Analytics.Support;

/// <summary>
/// The API over the recorded Data Dragon and a database holding 16.19.1, the latest version:
/// the pages the tests request exist or not as they do on the site. Its clock is the real
/// one, as the views' days are.
/// </summary>
/// <remarks>
/// The writer worker is off, as every worker in the tests: a test flushes the queue itself.
/// </remarks>
public sealed class AnalyticsApiFixture : IAsyncLifetime
{
    /// <summary>The address nginx mirrors every served page to.</summary>
    public const string MirrorPath = "/internal/analytics/page";

    /// <summary>The router's beacon.</summary>
    public const string BeaconPath = "/api/analytics/view";

    /// <summary>A reader's browser: a view from it is no bot's.</summary>
    public const string ReaderAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)"
        + " Chrome/126.0.0.0 Safari/537.36";

    private const string OriginalUriHeader = "X-Original-URI";
    private const string UserAgentHeader = "User-Agent";

    private readonly PostgresContainerFixture _postgres = new();
    private TestDatabase? _database;
    private ApiFactory? _factory;
    private WebApplicationFactory<Program>? _host;

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public IServiceProvider Services => Host.Services;

    private WebApplicationFactory<Program> Host =>
        _host ?? throw new InvalidOperationException("The fixture is not initialized.");

    private TestDatabase Database =>
        _database ?? throw new InvalidOperationException("The fixture is not initialized.");

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
                // The promotion of the ingestion below must be seen by the first view.
                ["LoDb:Catalog:VersionsLifetime"] = "00:00:00.001",
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        _host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddDdragonFixtureReplay(replay)));
        await IngestAsync(_host.Services.GetRequiredService<IVersionIngestion>());
    }

    /// <summary>A page served by nginx, as its mirror forwards it.</summary>
    public async Task<HttpResponseMessage> MirrorAsync(string? target)
    {
        using var client = Host.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(MirrorPath, UriKind.Relative));
        request.Headers.Add(UserAgentHeader, ReaderAgent);
        if (target is not null)
        {
            request.Headers.Add(OriginalUriHeader, target);
        }

        return await client.SendAsync(request, Token);
    }

    /// <summary>A navigation inside the application, as the router's beacon reports it.</summary>
    public Task<HttpResponseMessage> BeaconAsync(object body, string? origin = null) =>
        SendBeaconAsync(JsonContent.Create(body), origin);

    /// <summary>The beacon with a raw body.</summary>
    public async Task<HttpResponseMessage> SendBeaconAsync(HttpContent body, string? origin)
    {
        using var client = Host.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(BeaconPath, UriKind.Relative))
        {
            Content = body,
        };
        request.Headers.Add(UserAgentHeader, ReaderAgent);
        if (origin is not null)
        {
            request.Headers.Add(BrowserClient.OriginHeader, origin);
        }

        return await client.SendAsync(request, Token);
    }

    /// <summary>The views written so far, oldest first.</summary>
    public async Task<List<AnalyticsEvent>> EventsAsync()
    {
        await using var context = Database.CreateContext();
        return await context.AnalyticsEvents.AsNoTracking()
            .OrderBy(static view => view.Id)
            .ToListAsync(Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            // Disposes the host derived from it as well.
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
