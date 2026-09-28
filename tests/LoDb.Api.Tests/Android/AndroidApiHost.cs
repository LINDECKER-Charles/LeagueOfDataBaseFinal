using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Android;

/// <summary>
/// The API as the Android app meets it in production: on the site's public origin, over a
/// database of its own. The site origin is not the app's, so that nothing but the app's own
/// allowance lets <c>https://localhost</c> in.
/// </summary>
public sealed class AndroidApiHost : IAsyncDisposable
{
    /// <summary>Origin of the WebView that serves the embedded bundle (ADR 0007).</summary>
    public const string AppOrigin = "https://localhost";

    /// <summary>Public origin of the shell build (environment.shell.ts).</summary>
    public const string SiteOrigin = "https://league-of-data-base.com";

    public const string Username = "Mobile_42";
    public const string Password = "Str0ng-passphrase!";

    private const string Accounts = "LoDb:Accounts:";

    private readonly TestDatabase _database;
    private readonly ApiFactory _factory;

    private AndroidApiHost(TestDatabase database)
    {
        _database = database;
        _factory = new ApiFactory
        {
            PostgresConnectionString = database.ConnectionString,
            Settings = new Dictionary<string, string?>
            {
                [Accounts + "SecureCookies"] = bool.TrueString,
                [Accounts + "SiteOrigin"] = SiteOrigin,
            },
        };
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static async Task<AndroidApiHost> StartAsync(PostgresContainerFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.MigrateAsync(Cancellation);
        return new AndroidApiHost(database);
    }

    /// <summary>A client of the API on the site's host; the test sets the <c>Origin</c>.</summary>
    public HttpClient Client() => _factory.CreateDefaultClient(new Uri(SiteOrigin));

    /// <summary>A verified account with <see cref="Password"/>.</summary>
    public async Task SeedAccountAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            UserName = Username,
            Email = "mobile@example.test",
            EmailConfirmed = true,
            Roles = [],
            CreatedAt = TimeProvider.System.GetUtcNow(),
        };
        var created = await users.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Code)));
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}
