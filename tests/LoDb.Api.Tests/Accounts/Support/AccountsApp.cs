using System.Globalization;
using System.Text.Json;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Testing;
using MailKit.Security;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>
/// The API over a database of its own, with the doubles of the accounts: the e-mail outbox
/// records what it is given, unless a relay is given, Google answers from
/// <see cref="FakeGoogle"/>, and the clock only moves when a test advances it.
/// </summary>
/// <remarks>
/// One per test: the rate limits live in the host, the lockouts and throttles in the
/// database. Identity's lockout and e-mail tokens read the real clock, not this one.
/// </remarks>
public sealed class AccountsApp : IAsyncDisposable
{
    public const string SiteOrigin = "https://localhost";
    public const string WebClientId = "web.apps.googleusercontent.com";
    public const string WebClientSecret = "web-secret";
    public const string AndroidClientId = "android.apps.googleusercontent.com";
    public const string DesktopClientId = "desktop.apps.googleusercontent.com";
    public const string DesktopClientSecret = "desktop-secret";

    private const string Accounts = "LoDb:Accounts:";
    private const string Google = Accounts + "Google:";
    private const string Mail = "LoDb:Mail:";

    private readonly TestDatabase _database;
    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _host;
    private readonly bool _realOutbox;

    private AccountsApp(TestDatabase database, bool withGoogle, SmtpSink? relay)
    {
        _database = database;
        _realOutbox = relay is not null;
        _factory = new ApiFactory
        {
            PostgresConnectionString = database.ConnectionString,
            Clock = Clock,
            Settings = Settings(withGoogle, relay),
        };
        _host = _factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(AddDoubles));
    }

    public static Uri BaseAddress { get; } = new(SiteOrigin);

    /// <summary>Starts at the real time, so that the cookies it dates stay in the future.</summary>
    public FakeTimeProvider Clock { get; } = new(TimeProvider.System.GetUtcNow());

    /// <summary>What is queued; nothing when the real outbox sends to a relay.</summary>
    public RecordingOutbox Outbox { get; } = new();

    public FakeGoogle GoogleServer { get; } = new();

    public IServiceProvider Services => _host.Services;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <param name="postgres">The server the database of the host is created on.</param>
    /// <param name="withGoogle">Whether the Google clients are configured.</param>
    /// <param name="relay">
    /// The relay the real outbox of lot L4.3 sends to, when a test calls its dispatcher; the
    /// worker, whose wait follows the host's clock, takes no part once started.
    /// </param>
    public static async Task<AccountsApp> StartAsync(
        PostgresContainerFixture postgres,
        bool withGoogle = true,
        SmtpSink? relay = null)
    {
        var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.MigrateAsync(Cancellation);
        return new AccountsApp(database, withGoogle, relay);
    }

    /// <summary>A browser on the site: it keeps its cookies and follows no redirect.</summary>
    public BrowserClient Browser() => BrowserClient.Open(_host);

    /// <summary>A native app: no cookie, no <c>Origin</c>.</summary>
    public HttpClient App() => _host.CreateDefaultClient(BaseAddress);

    /// <summary>Creates the account through Identity, as a registration would.</summary>
    public Task<User> SeedAsync(AccountSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var user = new User
        {
            UserName = seed.Username,
            Email = seed.Email,
            EmailConfirmed = seed.EmailConfirmed,
            IsBanned = seed.Banned,
            GoogleId = seed.GoogleId,
            RiotTagline = seed.RiotTagline,
            Roles = [],
            CreatedAt = Clock.GetUtcNow(),
        };
        return UsersAsync(async users =>
        {
            var created = seed.Password is null
                ? await users.CreateAsync(user)
                : await users.CreateAsync(user, seed.Password);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Code)));
            return user;
        });
    }

    /// <summary>Runs <paramref name="action"/> with the user manager of its own scope.</summary>
    public async Task<T> UsersAsync<T>(Func<UserManager<User>, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = _host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<UserManager<User>>());
    }

    /// <summary>The account as stored now.</summary>
    public Task<User> FindAsync(string username) =>
        UsersAsync(async users => await users.FindByNameAsync(username)
            ?? throw new InvalidOperationException($"No account is named {username}."));

    /// <summary>Bans the account as the admin lot must: the flag, then a new stamp.</summary>
    public Task BanAsync(string username) =>
        UsersAsync(async users =>
        {
            var user = await users.FindByNameAsync(username);
            user!.IsBanned = true;
            user.BannedAt = Clock.GetUtcNow();
            return await users.UpdateSecurityStampAsync(user);
        });

    /// <summary>A new security stamp, as a password change elsewhere gives.</summary>
    public Task RenewStampAsync(string username) =>
        UsersAsync(async users =>
            await users.UpdateSecurityStampAsync((await users.FindByNameAsync(username))!));

    /// <summary>Turns on the authenticator of the account; its key and recovery codes.</summary>
    public Task<TwoFactorSecrets> EnableTwoFactorAsync(string username) =>
        UsersAsync(async users =>
        {
            var user = await users.FindByNameAsync(username);
            await users.ResetAuthenticatorKeyAsync(user!);
            await users.SetTwoFactorEnabledAsync(user!, enabled: true);
            var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user!, number: 10);
            var key = await users.GetAuthenticatorKeyAsync(user!);
            return new TwoFactorSecrets(key!, [.. codes!]);
        });

    /// <summary>The audit journal, oldest first.</summary>
    public async Task<IReadOnlyList<AuditLogEntry>> AuditAsync()
    {
        await using var context = _database.CreateContext();
        return await context.AuditLog.AsNoTracking()
            .OrderBy(static entry => entry.Id)
            .ToListAsync(Cancellation);
    }

    /// <summary>A field of the <c>meta</c> of a journal entry, or null.</summary>
    public static string? Meta(AuditLogEntry entry, string key)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Meta is null)
        {
            return null;
        }

        using var meta = JsonDocument.Parse(entry.Meta);
        return meta.RootElement.TryGetProperty(key, out var value) ? value.GetString() : null;
    }

    public Task<IReadOnlyList<string>> QueryAsync(string sql) =>
        _database.QueryAsync(sql, Cancellation);

    public async ValueTask DisposeAsync()
    {
        // Disposes the host derived from it as well.
        await _factory.DisposeAsync();
        GoogleServer.Dispose();
        await _database.DisposeAsync();
    }

    private static Dictionary<string, string?> Settings(bool withGoogle, SmtpSink? relay)
    {
        var settings = new Dictionary<string, string?>
        {
            [Accounts + "SecureCookies"] = bool.TrueString,
            [Accounts + "SiteOrigin"] = SiteOrigin,
        };
        if (withGoogle)
        {
            settings[Google + "ClientId"] = WebClientId;
            settings[Google + "ClientSecret"] = WebClientSecret;
            settings[Google + "AppClients:0:ClientId"] = AndroidClientId;
            settings[Google + "AppClients:1:ClientId"] = DesktopClientId;
            settings[Google + "AppClients:1:ClientSecret"] = DesktopClientSecret;
        }

        if (relay is not null)
        {
            settings[Mail + "Host"] = SmtpSink.Host;
            settings[Mail + "Port"] = relay.Port.ToString(CultureInfo.InvariantCulture);
            settings[Mail + "Security"] = nameof(SecureSocketOptions.None);
        }

        return settings;
    }

    private void AddDoubles(IServiceCollection services)
    {
        if (!_realOutbox)
        {
            services.AddSingleton<IEmailOutbox>(Outbox);
        }

        services.Configure<GoogleOptions>(
            GoogleDefaults.AuthenticationScheme,
            options => options.BackchannelHttpHandler = GoogleServer);
    }
}
