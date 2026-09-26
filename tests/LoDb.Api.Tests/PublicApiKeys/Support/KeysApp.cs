using System.Net.Http.Headers;
using LoDb.Api.Modules.PublicApi;
using LoDb.Api.Modules.PublicApi.Access;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Infrastructure.Persistence.PublicApi;
using LoDb.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.PublicApiKeys.Support;

/// <summary>
/// The API over a database of its own, <c>/v1</c> and its key cache as they run, the cache
/// spied on: a portal change shows on <c>/v1</c> only if the portal reports it.
/// </summary>
/// <remarks>One per test: the key cache and the rate limits live in the host.</remarks>
public sealed class KeysApp : IAsyncDisposable
{
    public const string PortalPath = "/api/account/api-key";
    public const string RegeneratePath = PortalPath + "/regenerate";
    public const string V1UsagePath = "/v1/usage";
    public const string SiteOrigin = "https://api.example.test/";

    private readonly TestDatabase _database;
    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _host;
    private SpyKeyCache? _cache;

    private KeysApp(TestDatabase database)
    {
        _database = database;
        _factory = new ApiFactory
        {
            PostgresConnectionString = database.ConnectionString,
            Clock = Clock,
            Settings = new Dictionary<string, string?>
            {
                ["LoDb:Accounts:SecureCookies"] = bool.TrueString,
                ["LoDb:Accounts:SiteOrigin"] = AccountsApp.SiteOrigin,
                ["LoDb:PublicApi:SiteOrigin"] = SiteOrigin,
            },
        };
        _host = _factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(SpyOnKeyCache));
    }

    /// <summary>Starts at the real time, so that the cookies it dates stay in the future.</summary>
    public FakeTimeProvider Clock { get; } = new(TimeProvider.System.GetUtcNow());

    /// <summary>What the portal reported to the cache of <c>/v1</c>.</summary>
    public SpyKeyCache KeyCache =>
        _cache ??= (SpyKeyCache)_host.Services.GetRequiredService<IApiKeyCache>();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static async Task<KeysApp> StartAsync(PostgresContainerFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.MigrateAsync(Cancellation);
        return new KeysApp(database);
    }

    /// <summary>A browser on the site, as the portal calls the API.</summary>
    public BrowserClient Browser() => BrowserClient.Open(_host);

    /// <summary>A browser signed in as <paramref name="account"/>.</summary>
    public async Task<BrowserClient> SignedInAsync(User account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var browser = Browser();
        using var response = await browser.SignInAsync(account.UserName!);
        Assert.True(response.IsSuccessStatusCode, $"Sign-in: {(int)response.StatusCode}");
        return browser;
    }

    /// <summary>What <c>/v1/usage</c> answers to <paramref name="secret"/>, as a key holder.</summary>
    public async Task<HttpResponseMessage> AskV1Async(string secret)
    {
        using var client = _host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, V1UsagePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return await client.SendAsync(request, Cancellation);
    }

    /// <summary>Creates an account through Identity, as a registration would.</summary>
    public async Task<User> SeedUserAsync(string username, bool verified = true)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            UserName = username,
            Email = username.ToLowerInvariant() + "@example.test",
            EmailConfirmed = verified,
            Roles = [],
            CreatedAt = Clock.GetUtcNow(),
        };
        var created = await users.CreateAsync(user, AccountSeed.StrongPassword);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Code)));
        return user;
    }

    /// <summary>A context on the database, to seed rows and read them back.</summary>
    public LoDbDbContext Database() => _database.CreateContext();

    /// <summary>The keys of <paramref name="userId"/>, oldest first.</summary>
    public async Task<List<ApiKey>> KeysOfAsync(int userId)
    {
        await using var db = Database();
        return await db.ApiKeys.AsNoTracking()
            .Where(key => key.UserId == userId)
            .OrderBy(static key => key.Id)
            .ToListAsync(Cancellation);
    }

    /// <summary>The journal, oldest entry first.</summary>
    public async Task<List<AuditLogEntry>> AuditAsync()
    {
        await using var db = Database();
        return await db.AuditLog.AsNoTracking()
            .OrderBy(static entry => entry.Id)
            .ToListAsync(Cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        // Disposes the host derived from it as well.
        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }

    private static void SpyOnKeyCache(IServiceCollection services)
    {
        services.RemoveAll<IApiKeyCache>();
        services.AddSingleton<IApiKeyCache>(static provider =>
            new SpyKeyCache(provider.GetRequiredService<ApiKeyDirectory>()));
    }
}
