using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using LoDb.Api.Hosting.SecurityReports;
using LoDb.Api.Hosting.Telemetry;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LoDb.Api.Tests.Hosting.SecurityReports;

/// <summary>
/// <c>POST /api/csp-reports</c> counts what browsers report, in both formats, within its
/// bounds: size, type, the page's origin, a share per client and a bound for every client.
/// </summary>
public sealed class CspReportEndpointTests
{
    private const string ReportsPath = "/api/csp-reports";
    private const string ReportingType = "application/reports+json";
    private const string LegacyType = "application/csp-report";
    private const string SiteOrigin = "http://localhost";

    private const string Batch = """
        [
          {"type": "csp-violation", "age": 3, "url": "https://site.test/en/u/someone",
           "body": {"effectiveDirective": "script-src-elem", "disposition": "enforce",
                    "blockedURL": "inline", "documentURL": "https://site.test/en/u/someone"}},
          {"type": "deprecation", "body": {"id": "x"}},
          {"type": "csp-violation",
           "body": {"effectiveDirective": "require-trusted-types-for", "disposition": "report"}}
        ]
        """;

    private const string Legacy = """
        {"csp-report": {"document-uri": "https://site.test/b/token",
                        "violated-directive": "img-src 'self' data:"}}
        """;

    [Fact]
    public async Task CountsTheViolationsOfABatchAndNothingElse()
    {
        await using var factory = new ApiFactory();
        using var collector = new MetricCollector<long>(
            factory.Services.GetRequiredService<IMeterFactory>(),
            BuildInfoMetrics.MeterName,
            CspReportMetrics.CounterName);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, Batch, ReportingType);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var window = Drain(factory);
        Assert.Equal(2, window.Violations.Count);
        Assert.Equal(1, window.Violations[new("script-src-elem", "enforce")]);
        Assert.Equal(1, window.Violations[new("require-trusted-types-for", "report")]);
        Assert.Equal(2, collector.GetMeasurementSnapshot().Count);
    }

    [Fact]
    public async Task ReadsTheLegacyFormatByItsViolatedDirective()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, Legacy, LegacyType);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var violation = Assert.Single(Drain(factory).Violations);
        Assert.Equal(new CspViolation("img-src", "unknown"), violation.Key);
    }

    [Theory]
    [InlineData("text/plain", Legacy, HttpStatusCode.UnsupportedMediaType)]
    [InlineData(ReportingType, "{\"not\": \"a report\"}", HttpStatusCode.BadRequest)]
    [InlineData(ReportingType, "[{\"type\": ", HttpStatusCode.BadRequest)]
    public async Task RefusesWhatIsNotAReport(string type, string body, HttpStatusCode status)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, body, type);

        Assert.Equal(status, response.StatusCode);
        var window = Drain(factory);
        Assert.Empty(window.Violations);
        Assert.Equal(1, Assert.Single(window.Refusals).Value);
    }

    [Fact]
    public async Task RefusesAndCountsABodyPastItsBound()
    {
        await using var factory = Bounded("MaxBodyBytes", "64");
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, Batch, ReportingType);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        var refusal = Assert.Single(Drain(factory).Refusals);
        Assert.Equal(CspReportRefusal.Oversized, refusal.Key);
    }

    [Fact]
    public async Task RefusesAReportFromAnotherSite()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, Legacy, LegacyType, "https://evil.test");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(Drain(factory).IsEmpty);
    }

    [Fact]
    public async Task HoldsAClientToItsShare()
    {
        await using var factory = Bounded("PerClientPerMinute", "2");
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var sent = 0; sent < 3; sent++)
        {
            using var response = await PostAsync(client, Legacy, LegacyType);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(
            [HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.TooManyRequests],
            statuses);
    }

    [Fact]
    public async Task ShedsWhatEveryClientTogetherSendsPastTheBound()
    {
        await using var factory = Bounded("PerMinute", "1");
        using var client = factory.CreateClient();

        using var first = await PostAsync(client, Legacy, LegacyType);
        using var second = await PostAsync(client, Legacy, LegacyType);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("60", second.Headers.RetryAfter?.ToString());
        var window = Drain(factory);
        Assert.Equal(1, window.Shed);
        Assert.Single(window.Violations);
    }

    [Fact]
    public async Task ABoundOfZeroStopsTheHostAtStart()
    {
        await using var factory = Bounded("PerMinute", "0");

        var exception = Assert.ThrowsAny<Exception>(() => factory.Server);

        Assert.Contains("PerMinute", exception.ToString(), StringComparison.Ordinal);
    }

    private static ApiFactory Bounded(string key, string value) =>
        new()
        {
            Settings = new Dictionary<string, string?>
            {
                [$"{SecurityReportsOptions.SectionName}:{key}"] = value,
            },
        };

    private static CspReportWindow Drain(ApiFactory factory) =>
        factory.Services.GetRequiredService<CspReportTally>().Drain();

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string body,
        string type,
        string origin = SiteOrigin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ReportsPath)
        {
            Content = new StringContent(body, Encoding.UTF8, type),
        };
        request.Headers.Add("Origin", origin);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
