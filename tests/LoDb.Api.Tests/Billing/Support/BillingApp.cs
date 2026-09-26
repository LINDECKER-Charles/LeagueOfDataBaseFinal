using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Fulfilment;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Api.Modules.Billing.Webhooks;
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

namespace LoDb.Api.Tests.Billing.Support;

/// <summary>
/// The API over a database of its own, Stripe replaced by doubles: the gateway records the
/// sessions it is asked to open, the key cache what it is told to forget, and the handler of
/// completed sessions can be made to fail once, after its writes.
/// </summary>
/// <remarks>
/// One per test: the rate limits live in the host. The secrets are placeholders; no call
/// leaves the machine.
/// </remarks>
public sealed class BillingApp : IAsyncDisposable
{
    public const string WebhookSecret = "whsec_placeholder_for_tests";
    public const string SecretKey = "sk_test_placeholder_for_tests";
    public const string WebhookPath = "/webhooks/stripe";

    private const string Billing = "LoDb:Billing:";
    private const string Accounts = "LoDb:Accounts:";

    private readonly TestDatabase _database;
    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _host;

    private BillingApp(TestDatabase database, bool configured)
    {
        _database = database;
        Gateway.IsConfigured = configured;
        _factory = new ApiFactory
        {
            PostgresConnectionString = database.ConnectionString,
            Clock = Clock,
            Settings = new Dictionary<string, string?>
            {
                [Accounts + "SecureCookies"] = bool.TrueString,
                [Accounts + "SiteOrigin"] = AccountsApp.SiteOrigin,
                [Billing + "StripeSecretKey"] = configured ? SecretKey : null,
                [Billing + "StripeWebhookSecret"] = configured ? WebhookSecret : null,
            },
        };
        _host = _factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(AddDoubles));
    }

    /// <summary>Starts at the real time, so that the cookies it dates stay in the future.</summary>
    public FakeTimeProvider Clock { get; } = new(TimeProvider.System.GetUtcNow());

    public RecordingGateway Gateway { get; } = new();

    public RecordingKeyCache KeyCache { get; } = new();

    /// <summary>Armed, the next completed session fails once its handler has written.</summary>
    public HandlerFault Fault { get; } = new();

    public IServiceProvider Services => _host.Services;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <param name="postgres">The server the database of the host is created on.</param>
    /// <param name="configured">Whether the Stripe key and webhook secret are set.</param>
    public static async Task<BillingApp> StartAsync(
        PostgresContainerFixture postgres,
        bool configured = true)
    {
        var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.MigrateAsync(Cancellation);
        return new BillingApp(database, configured);
    }

    /// <summary>A browser on the site, as the donation page posts.</summary>
    public BrowserClient Browser() => BrowserClient.Open(_host);

    /// <summary>A context on the database, to seed rows and read them back.</summary>
    public LoDbDbContext Database() => _database.CreateContext();

    /// <summary>Posts <paramref name="stripeEvent"/> as Stripe does, signed at this time.</summary>
    public Task<HttpResponseMessage> DeliverAsync(JsonObject stripeEvent)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        var payload = stripeEvent.ToJsonString();
        var signature = WebhookSigner.Sign(payload, Clock.GetUtcNow(), WebhookSecret);
        return PostWebhookAsync(payload, signature);
    }

    /// <summary>Posts <paramref name="payload"/> with no cookie nor <c>Origin</c>.</summary>
    /// <param name="payload">The body, sent as is.</param>
    /// <param name="signature">The <c>Stripe-Signature</c> header; none when null.</param>
    public async Task<HttpResponseMessage> PostWebhookAsync(string payload, string? signature)
    {
        using var stripe = _host.CreateDefaultClient(AccountsApp.BaseAddress);
        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new StringContent(payload, Encoding.UTF8, MediaTypeNames.Application.Json),
        };
        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation(WebhookSigner.Header, signature);
        }

        return await stripe.SendAsync(request, Cancellation);
    }

    /// <summary>Creates an account through Identity, as a registration would.</summary>
    public async Task<User> SeedUserAsync(string username)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            UserName = username,
            Email = username.ToLowerInvariant() + "@example.test",
            EmailConfirmed = true,
            Roles = [],
            CreatedAt = Clock.GetUtcNow(),
        };
        var created = await users.CreateAsync(user, AccountSeed.StrongPassword);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Code)));
        return user;
    }

    /// <summary>Gives <paramref name="owner"/> a free active key, shaped as a test needs.</summary>
    public async Task<ApiKey> SeedKeyAsync(User owner, Action<ApiKey>? shape = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var key = new ApiKey
        {
            Name = "default",
            KeyHash = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)),
            KeyPrefix = "lodb_0123456",
            Plan = "free",
            MonthlyQuota = 500,
            RateLimitPerMin = 10,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow(),
            UserId = owner.Id,
        };
        shape?.Invoke(key);
        await using var db = Database();
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(Cancellation);
        return key;
    }

    /// <summary>Adds <paramref name="grants"/> as they are.</summary>
    public async Task SeedGrantsAsync(params ApiCreditGrant[] grants)
    {
        await using var db = Database();
        db.ApiCreditGrants.AddRange(grants);
        await db.SaveChangesAsync(Cancellation);
    }

    /// <summary>The keys of <paramref name="userId"/>, oldest first.</summary>
    public async Task<List<ApiKey>> KeysOfAsync(int userId)
    {
        await using var db = Database();
        return await db.ApiKeys.AsNoTracking()
            .Where(key => key.UserId == userId)
            .OrderBy(static key => key.Id)
            .ToListAsync(Cancellation);
    }

    /// <summary>The key <paramref name="id"/> as it is now.</summary>
    public async Task<ApiKey> KeyAsync(int id)
    {
        await using var db = Database();
        return await db.ApiKeys.AsNoTracking().SingleAsync(key => key.Id == id, Cancellation);
    }

    /// <summary>The grants of the key <paramref name="keyId"/>, oldest first.</summary>
    public async Task<List<ApiCreditGrant>> GrantsOfAsync(int keyId)
    {
        await using var db = Database();
        return await db.ApiCreditGrants.AsNoTracking()
            .Where(grant => grant.ApiKeyId == keyId)
            .OrderBy(static grant => grant.Id)
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

    private void AddDoubles(IServiceCollection services)
    {
        services.RemoveAll<ICheckoutGateway>();
        services.AddSingleton<ICheckoutGateway>(Gateway);
        services.RemoveAll<IApiKeyCache>();
        services.AddSingleton<IApiKeyCache>(KeyCache);
        services.AddSingleton(Fault);
        services.AddScoped<CheckoutCompletedHandler>();
        services.RemoveAll<IStripeEventHandler>();
        services.AddScoped<IStripeEventHandler, FaultyCheckoutHandler>();
        services.AddScoped<IStripeEventHandler, SubscriptionDeletedHandler>();
    }
}
