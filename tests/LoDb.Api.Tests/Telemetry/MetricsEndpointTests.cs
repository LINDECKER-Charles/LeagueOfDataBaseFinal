using System.Net;
using LoDb.Testing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LoDb.Api.Tests.Telemetry;

/// <summary>
/// The Prometheus scrape is served on the metrics port only, and that port serves nothing
/// else: nginx, which only reaches the API port, can never expose it.
/// </summary>
public sealed class MetricsEndpointTests
{
    private const string Revision = "0123abcd";
    private static readonly Uri Metrics = new("/metrics", UriKind.Relative);
    private static readonly Uri MetricsPort = new("http://localhost:9464");
    private static readonly Uri ApiPort = new("http://localhost:8080");

    [Fact]
    public async Task MetricsPortServesTheBuildInfoGauge()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, MetricsPort);

        using var response = await client.GetAsync(Metrics, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var buildInfo = body.Split('\n')
            .Where(static line => line.StartsWith("lodb_build_info{", StringComparison.Ordinal));
        Assert.Contains(buildInfo, static line =>
            line.Contains($"revision=\"{Revision}\"", StringComparison.Ordinal)
            && line.Contains("version=\"", StringComparison.Ordinal)
            && line.EndsWith(" 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApiPortDoesNotServeMetrics()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, ApiPort);

        using var response = await client.GetAsync(Metrics, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MetricsPortServesNothingButTheScrape()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, MetricsPort);

        using var response = await client.GetAsync(
            new Uri("/healthz", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static ApiFactory CreateFactory() => new()
    {
        Settings = new Dictionary<string, string?> { ["APP_REVISION"] = Revision },
    };

    private static HttpClient CreateClient(ApiFactory factory, Uri baseAddress) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = baseAddress });
}
