using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using LoDb.Testing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LoDb.Api.Tests.Telemetry;

/// <summary>
/// What the scrape takes besides the LoDb meters: the framework's security meters, which the
/// ASP.NET Core instrumentation leaves out, and a request duration histogram held to 8
/// bounds, each bound being one more series per route, method and status.
/// </summary>
public sealed partial class TelemetryMetricsTests
{
    private const string ProbeCounter = "lodb.tests.meter_probe";
    private static readonly Uri Metrics = new("/metrics", UriKind.Relative);
    private static readonly Uri Health = new("/healthz", UriKind.Relative);
    private static readonly Uri MetricsPort = new("http://localhost:9464");
    private static readonly Uri ApiPort = new("http://localhost:8080");

    [Theory]
    [InlineData("Microsoft.AspNetCore.Identity")]
    [InlineData("Microsoft.AspNetCore.Authentication")]
    [InlineData("Microsoft.AspNetCore.Authorization")]
    public async Task FrameworkSecurityMeterReachesTheScrape(string meterName)
    {
        await using var factory = new ApiFactory();
        using var scraper = Client(factory, MetricsPort);
        using var meter = new Meter(meterName);
        meter.CreateCounter<long>(ProbeCounter).Add(1);

        var exposition = await ScrapeAsync(scraper);

        Assert.Contains(
            $"lodb_tests_meter_probe_total{{otel_scope_name=\"{meterName}\"",
            exposition,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestDurationKeepsEightBounds()
    {
        await using var factory = new ApiFactory();
        using var api = Client(factory, ApiPort);
        using var scraper = Client(factory, MetricsPort);
        using var health = await api.GetAsync(Health, TestContext.Current.CancellationToken);

        var exposition = await ScrapeAsync(scraper);

        var bounds = exposition.Split('\n')
            .Where(static line => line.StartsWith(
                "http_server_request_duration_seconds_bucket{",
                StringComparison.Ordinal))
            .Where(static line => line.Contains("/healthz", StringComparison.Ordinal))
            .Select(static line => BucketBound().Match(line).Groups[1].Value)
            .Distinct()
            .ToList();
        Assert.Equal(["0.01", "0.05", "0.1", "0.25", "0.5", "1", "2.5", "10", "+Inf"], bounds);
    }

    private static HttpClient Client(ApiFactory factory, Uri baseAddress) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = baseAddress });

    private static async Task<string> ScrapeAsync(HttpClient scraper)
    {
        using var response = await scraper.GetAsync(
            Metrics,
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [GeneratedRegex("le=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex BucketBound();
}
