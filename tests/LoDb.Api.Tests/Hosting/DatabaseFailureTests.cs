using System.Diagnostics.Metrics;
using System.Net;
using LoDb.Api.Hosting.Errors;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Npgsql;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// A request the database fails: a pool that stays busy sheds it with a 503 the client may
/// retry, a server out of reach answers a 500 and is counted, since it is an outage.
/// </summary>
public sealed class DatabaseFailureTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>
{
    // A share token no build has: the read goes to the database, which answers 404.
    private const string SharedBuildPath = "/api/share/ffffffffffffffffffffffff";

    private static readonly Uri MetricsPort = new("http://localhost:9464");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BusyPoolAnswers503WithRetryAfter()
    {
        await using var database = await postgres.CreateDatabaseAsync(Cancellation);
        await database.MigrateAsync(Cancellation);
        await using var factory = new ApiFactory
        {
            PostgresConnectionString = SingleConnectionPool(database.ConnectionString),
            Settings = new Dictionary<string, string?>
            {
                ["LoDb:Hosting:DatabaseBusyRetryAfter"] = "00:00:07",
            },
        };
        using var client = factory.CreateClient();
        using var unavailable = UnavailableCounter(factory);

        await using (await factory.Services.GetRequiredService<NpgsqlDataSource>()
            .OpenConnectionAsync(Cancellation))
        {
            using var busy = await client.GetAsync(SharedBuildPath, Cancellation);

            Assert.Equal(
                "service-busy",
                await ApiJson.ProblemCodeAsync(busy, HttpStatusCode.ServiceUnavailable));
            Assert.Equal("7", busy.Headers.RetryAfter?.ToString());
        }

        using var freed = await client.GetAsync(SharedBuildPath, Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, freed.StatusCode);
        Assert.Empty(unavailable.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task UnreachableDatabaseAnswers500AndIsCounted()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var unavailable = UnavailableCounter(factory);

        using var response = await client.GetAsync(SharedBuildPath, Cancellation);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ApiJson.ProblemMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("Retry-After"));
        Assert.Equal(1, Assert.Single(unavailable.GetMeasurementSnapshot()).Value);
        Assert.Contains(
            (await ScrapeAsync(factory)).Split('\n'),
            static line => line.StartsWith("lodb_db_unavailable_total", StringComparison.Ordinal)
                && line.EndsWith(" 1", StringComparison.Ordinal));
    }

    // One connection, and a wait of one second for it.
    private static string SingleConnectionPool(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = 1,
            Timeout = 1,
        }.ConnectionString;

    private static MetricCollector<long> UnavailableCounter(ApiFactory factory) => new(
        factory.Services.GetRequiredService<IMeterFactory>(),
        DatabaseMetrics.MeterName,
        DatabaseMetrics.UnavailableName);

    private static async Task<string> ScrapeAsync(ApiFactory factory)
    {
        using var metrics = factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = MetricsPort });
        return await metrics.GetStringAsync(new Uri("/metrics", UriKind.Relative), Cancellation);
    }
}
