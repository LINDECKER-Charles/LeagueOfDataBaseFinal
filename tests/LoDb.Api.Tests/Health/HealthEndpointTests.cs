using System.Net;
using System.Text.Json;
using LoDb.Testing;

namespace LoDb.Api.Tests.Health;

/// <summary>
/// Liveness never depends on anything; readiness fails as soon as the database or the
/// storage root is out of reach.
/// </summary>
/// <remarks>
/// The shared host has no database and a storage root: the test of a missing root builds its
/// own.
/// </remarks>
public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Uri Liveness = new("/healthz", UriKind.Relative);
    private static readonly Uri Readiness = new("/readyz", UriKind.Relative);

    [Fact]
    public async Task LivenessAnswers200WithoutAnyDependency()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            Liveness,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadBodyAsync(response);
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ReadinessAnswers503WithoutDatabase()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            Readiness,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = await ReadBodyAsync(response);
        var checks = body.RootElement.GetProperty("checks");
        Assert.Equal("Unhealthy", checks.GetProperty("postgres").GetString());
        Assert.Equal("Healthy", checks.GetProperty("storage").GetString());
    }

    [Fact]
    public async Task ReadinessAnswers503WhenTheStorageRootIsMissing()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), $"lodb-missing-{Guid.NewGuid():N}");
        await using var rootless = new ApiFactory
        {
            Settings = new Dictionary<string, string?> { ["LoDb:Storage:Root"] = missingRoot },
        };
        using var client = rootless.CreateClient();

        using var response = await client.GetAsync(
            Readiness,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = await ReadBodyAsync(response);
        var storage = body.RootElement.GetProperty("checks").GetProperty("storage");
        Assert.Equal("Unhealthy", storage.GetString());
        Assert.False(Directory.Exists(missingRoot));
    }

    internal static async Task<JsonDocument> ReadBodyAsync(HttpResponseMessage response)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
    }
}
