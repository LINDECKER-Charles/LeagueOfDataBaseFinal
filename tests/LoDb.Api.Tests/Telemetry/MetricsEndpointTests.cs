using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LoDb.Api.Tests.Telemetry;

/// <summary>
/// The Prometheus scrape is served on the metrics port only, and that port serves nothing
/// else: nginx, which only reaches the API port, can never expose it.
/// </summary>
public sealed class MetricsEndpointTests(RevisedApi api) : IClassFixture<RevisedApi>
{
    private static readonly Uri Metrics = new("/metrics", UriKind.Relative);
    private static readonly Uri MetricsPort = new("http://localhost:9464");
    private static readonly Uri ApiPort = new("http://localhost:8080");

    [Fact]
    public async Task MetricsPortServesTheBuildInfoGauge()
    {
        using var client = CreateClient(MetricsPort);

        using var response = await client.GetAsync(Metrics, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var buildInfo = body.Split('\n')
            .Where(static line => line.StartsWith("lodb_build_info{", StringComparison.Ordinal));
        Assert.Contains(buildInfo, static line =>
            line.Contains($"revision=\"{RevisedApi.Revision}\"", StringComparison.Ordinal)
            && line.Contains("version=\"", StringComparison.Ordinal)
            && line.EndsWith(" 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApiPortDoesNotServeMetrics()
    {
        using var client = CreateClient(ApiPort);

        using var response = await client.GetAsync(Metrics, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MetricsPortServesNothingButTheScrape()
    {
        using var client = CreateClient(MetricsPort);

        using var response = await client.GetAsync(
            new Uri("/healthz", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient(Uri baseAddress) =>
        api.Factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = baseAddress });
}
