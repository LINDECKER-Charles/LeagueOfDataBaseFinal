using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Infrastructure.Persistence.Builds;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Builds.Support;

/// <summary>
/// The API over the recorded Data Dragon and one database holding the latest patch, ingested
/// whole; the other recorded patches are ingested on the first call that reads them.
/// </summary>
/// <remarks>
/// The tests share the database: each one seeds accounts of its own, named by
/// <see cref="SeedAsync"/>, and never relies on another's builds.
/// </remarks>
public sealed class BuildsApp : IAsyncLifetime
{
    private const string Accounts = "LoDb:Accounts:";
    private const string ImagePath = "/img/";

    private readonly PostgresContainerFixture _postgres = new();
    private TestDatabase? _database;
    private ApiFactory? _factory;
    private WebApplicationFactory<Program>? _host;
    private int _seeded;

    /// <summary>The newest recorded patch, ingested before the tests.</summary>
    public static PatchVersion Latest => DdragonFixtures.Latest;

    /// <summary>The patch before it, recorded with the same datasets.</summary>
    public static PatchVersion Previous => DdragonFixtures.Previous;

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
        var replay = Replay();
        _host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddDdragonFixtureReplay(replay);
            services.AddSingleton<IEmailOutbox>(new RecordingOutbox());

            // As in production: a body that does not bind is a 400, not an exception.
            services.Configure<RouteHandlerOptions>(static options =>
                options.ThrowOnBadRequest = false);
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
            Username = "Batisseur_" + number,
            Email = $"batisseur{number}@example.test",
        };
        await using var scope = Host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            UserName = named.Username,
            Email = named.Email,
            EmailConfirmed = named.EmailConfirmed,
            IsBanned = named.Banned,
            Roles = [],
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var created = named.Password is null
            ? await users.CreateAsync(user)
            : await users.CreateAsync(user, named.Password);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Code)));
        return named;
    }

    /// <summary>
    /// Stores one vote per value on a build, each by a new account without a password: the
    /// way to score the builds a ranking orders.
    /// </summary>
    public async Task InsertVotesAsync(int buildId, params short[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            var voter = await SeedAsync(new AccountSeed { Password = null });
            await using var context = Database.CreateContext();
            var voterId = await context.Users
                .Where(user => user.UserName == voter.Username)
                .Select(static user => user.Id)
                .SingleAsync(Token);
            context.BuildVotes.Add(new BuildVote
            {
                BuildId = buildId,
                VoterId = voterId,
                Value = value,
                CreatedAt = DateTimeOffset.UnixEpoch,
            });
            await context.SaveChangesAsync(Token);
        }
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

    /// <summary>
    /// Inserts a build of <paramref name="username"/> as the legacy stack stored it, bypassing
    /// every rule: the way to seed ghosts, old patches and rankings.
    /// </summary>
    public async Task<Build> InsertAsync(string username, StoredBuild stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        await using var context = Database.CreateContext();
        var owner = await context.Users.SingleAsync(user => user.UserName == username, Token);
        var build = stored.ToEntity(owner.Id);
        context.Builds.Add(build);
        await context.SaveChangesAsync(Token);
        return build;
    }

    /// <summary>The build as stored now; null once deleted.</summary>
    public async Task<Build?> FindBuildAsync(int id)
    {
        await using var context = Database.CreateContext();
        return await context.Builds.AsNoTracking()
            .SingleOrDefaultAsync(build => build.Id == id, Token);
    }

    /// <summary>The votes stored for a build.</summary>
    public async Task<IReadOnlyList<BuildVote>> VotesAsync(int buildId)
    {
        await using var context = Database.CreateContext();
        return await context.BuildVotes.AsNoTracking()
            .Where(vote => vote.BuildId == buildId)
            .ToListAsync(Token);
    }

    /// <summary>
    /// The journal lines about builds <paramref name="username"/> caused, oldest first.
    /// </summary>
    public async Task<IReadOnlyList<AuditLogEntry>> AuditAsync(string username)
    {
        await using var context = Database.CreateContext();
        return await context.AuditLog.AsNoTracking()
            .Where(entry => entry.Actor == username && entry.TargetType == AuditTargetType.Build)
            .OrderBy(static entry => entry.Id)
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

    // Only the images of the newest patches are recorded: the others answer as the images a
    // patch lacks, so a build pinned to an old patch renders with absent icons.
    private static FixtureReplayHandler Replay()
    {
        var replay = DdragonFixtures.CreateReplay();
        var recorded = DdragonFixtures.Index.Responses
            .Select(static response => new Uri(response.Url).AbsoluteUri)
            .ToHashSet(StringComparer.Ordinal);
        replay.FailWith(
            url => url.AbsolutePath.Contains(ImagePath, StringComparison.Ordinal)
                && !recorded.Contains(url.AbsoluteUri),
            HttpStatusCode.NotFound);
        return replay;
    }
}
