using System.Net;
using LoDb.Testing;

namespace LoDb.Api.Tests.Health;

/// <summary>
/// With a real PostgreSQL server and a writable storage root, the API is ready.
/// </summary>
public sealed class ReadinessWithDatabaseTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task ReadinessAnswers200WithDatabaseAndStorage()
    {
        await using var factory = new ApiFactory
        {
            PostgresConnectionString = postgres.ConnectionString,
        };
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/readyz", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await HealthEndpointTests.ReadBodyAsync(response);
        var checks = body.RootElement.GetProperty("checks");
        Assert.Equal("Healthy", checks.GetProperty("postgres").GetString());
        Assert.Equal("Healthy", checks.GetProperty("storage").GetString());
        Assert.Empty(Directory.EnumerateFileSystemEntries(factory.StorageRoot));
    }
}
