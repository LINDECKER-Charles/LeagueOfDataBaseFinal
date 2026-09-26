using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Profiles.Support;

/// <summary>
/// The API over the recorded Data Dragon and one database holding the latest patch, ingested
/// whole. The previous patch is broken on purpose: every file of it fails, so a profile that
/// pins it has no catalog to read.
/// </summary>
/// <remarks>
/// The tests share the database: each one seeds accounts of its own, named by
/// <see cref="SeedAsync"/>, and never relies on another's.
/// </remarks>
public sealed class ProfilesApp : IAsyncLifetime
{
    private const string Accounts = "LoDb:Accounts:";

    private readonly PostgresContainerFixture _postgres = new();
    private TestDatabase? _database;
    private ApiFactory? _factory;
    private WebApplicationFactory<Program>? _host;
    private int _seeded;

    /// <summary>The version the profiles read: recorded and ingested.</summary>
    public static PatchVersion Latest => DdragonFixtures.Latest;

    /// <summary>A version Data Dragon lists but whose files all fail.</summary>
    public static PatchVersion Broken => DdragonFixtures.Previous;

    public static CancellationToken Token => TestContext.Current.CancellationToken;

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
                [Accounts + "SecureCookies"] = bool.TrueString,
                [Accounts + "SiteOrigin"] = AccountsApp.SiteOrigin,
                ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001",
                ["LoDb:Catalog:VersionsLifetime"] = "00:00:00.001",
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        var broken = $"/cdn/{Broken.Value}/";
        replay.FailWith(
            url => url.AbsolutePath.Contains(broken, StringComparison.Ordinal),
            HttpStatusCode.ServiceUnavailable);
        _host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddDdragonFixtureReplay(replay);
            services.AddSingleton<IEmailOutbox>(new RecordingOutbox());
        }));
        var ingestion = _host.Services.GetRequiredService<IVersionIngestion>();
        var ingested = await ingestion.IngestAsync(Latest, IngestionRequest.Complete, Token);
        Assert.Equal(VersionIngestionOutcome.Completed, ingested.Outcome);
    }

    /// <summary>A browser on the site: it keeps its cookies and follows no redirect.</summary>
    public BrowserClient Browser() => BrowserClient.Open(Host);

    /// <summary>
    /// Creates an account of a name no other test uses: <paramref name="seed"/> with its
    /// username and e-mail replaced.
    /// </summary>
    public async Task<AccountSeed> SeedAsync(AccountSeed? seed = null)
    {
        var number = Interlocked.Increment(ref _seeded).ToString(CultureInfo.InvariantCulture);
        var named = (seed ?? new AccountSeed()) with
        {
            Username = "Joueur_" + number,
            Email = $"joueur{number}@example.test",
        };
        var user = new User
        {
            UserName = named.Username,
            Email = named.Email,
            EmailConfirmed = named.EmailConfirmed,
            IsBanned = named.Banned,
            GoogleId = named.GoogleId,
            Roles = [],
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await UsersAsync(async users =>
        {
            var created = named.Password is null
                ? await users.CreateAsync(user)
                : await users.CreateAsync(user, named.Password);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Code)));
            return user;
        });
        return named;
    }

    /// <summary>A browser signed in to <paramref name="account"/> with its password.</summary>
    public async Task<BrowserClient> SignInAsync(AccountSeed account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var browser = Browser();
        using var signedIn = await browser.SignInAsync(account.Username);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        return browser;
    }

    /// <summary>Changes the stored account directly, its security stamp untouched.</summary>
    public async Task UpdateAsync(string username, Action<User> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        await using var context = Database.CreateContext();
        var user = await context.Users.SingleAsync(user => user.UserName == username, Token);
        change(user);
        await context.SaveChangesAsync(Token);
    }

    /// <summary>The account as stored now; null once erased.</summary>
    public async Task<User?> FindAsync(string username)
    {
        await using var context = Database.CreateContext();
        return await context.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.UserName == username, Token);
    }

    /// <summary>Bans the account as the admin lot must: the flag, then a new stamp.</summary>
    public Task BanAsync(string username) =>
        UsersAsync(async users =>
        {
            var user = await users.FindByNameAsync(username);
            user!.IsBanned = true;
            user.BannedAt = DateTimeOffset.UtcNow;
            return await users.UpdateSecurityStampAsync(user);
        });

    /// <summary>The journal lines about <paramref name="username"/>, oldest first.</summary>
    public async Task<IReadOnlyList<AuditLogEntry>> AuditAsync(string username)
    {
        await using var context = Database.CreateContext();
        return await context.AuditLog.AsNoTracking()
            .Where(entry => entry.Actor == username || entry.Target == username)
            .OrderBy(static entry => entry.Id)
            .ToListAsync(Token);
    }

    /// <summary>Runs <paramref name="action"/> on a context of its own.</summary>
    public async Task WithContextAsync(Func<LoDbDbContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var context = Database.CreateContext();
        await action(context);
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

    private async Task<T> UsersAsync<T>(Func<UserManager<User>, Task<T>> action)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<UserManager<User>>());
    }
}
