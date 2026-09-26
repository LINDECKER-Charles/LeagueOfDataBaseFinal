using System.Diagnostics.Metrics;
using LoDb.Api.Modules.ClientPolicy.Gate;
using LoDb.Infrastructure.Persistence.Apps;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LoDb.Api.Tests.ClientPolicy.Support;

/// <summary>
/// The API over a database of its own, whose <c>client_policy</c> a test fills directly.
/// </summary>
public sealed class ClientPolicyApp : IAsyncDisposable
{
    /// <summary>Origin of the Android WebView, the one cross-origin caller of /api.</summary>
    public const string AndroidOrigin = "https://localhost";

    private readonly TestDatabase _database;
    private readonly ApiFactory _factory;

    private ClientPolicyApp(TestDatabase database)
    {
        _database = database;
        _factory = new ApiFactory { PostgresConnectionString = database.ConnectionString };
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static async Task<ClientPolicyApp> StartAsync(PostgresContainerFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.MigrateAsync(Cancellation);
        return new ClientPolicyApp(database);
    }

    public HttpClient Client() => _factory.CreateClient();

    /// <summary>Stores a policy row, before the host first reads (and caches) the policy.</summary>
    public async Task SeedAsync(params ClientPolicyEntry[] rows)
    {
        await using var context = _database.CreateContext();
        context.ClientPolicies.AddRange(rows);
        await context.SaveChangesAsync(Cancellation);
    }

    /// <summary>Counts the 426 answers of this host from now on.</summary>
    public MetricCollector<long> UpgradeRequiredCounter() => new(
        _factory.Services.GetRequiredService<IMeterFactory>(),
        ClientPolicyMetrics.MeterName,
        "lodb.client_policy.upgrade_required");

    public static ClientPolicyEntry Policy(AppPlatform platform, string? minimum, string? latest) =>
        new()
        {
            Platform = platform,
            MinimumVersion = minimum,
            LatestVersion = latest,
            PublishedAt = TimeProvider.System.GetUtcNow(),
        };

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}
