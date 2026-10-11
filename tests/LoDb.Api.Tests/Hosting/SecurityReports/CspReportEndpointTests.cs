using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Text;
using LoDb.Api.Hosting.SecurityReports;
using LoDb.Api.Hosting.SecurityReports.Reports;
using LoDb.Api.Hosting.SecurityReports.Summary;
using LoDb.Api.Hosting.Telemetry;
using LoDb.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LoDb.Api.Tests.Hosting.SecurityReports;

/// <summary>
/// <c>POST /api/csp-reports</c> counts what browsers report, in both formats, within its
/// bounds: size, type, the page's origin, a share per client and a bound for every client.
/// </summary>
/// <remarks>
/// The tests of the default bounds share one host, each from an empty window; the bounds
/// and the shares of the clients, host state, get a host of their own.
/// </remarks>
public sealed class CspReportEndpointTests : IClassFixture<ApiFactory>
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

    private readonly ApiFactory _shared;

    public CspReportEndpointTests(ApiFactory shared)
    {
        _shared = shared;
        Drain(shared);
    }

    [Fact]
    public async Task CountsTheViolationsOfABatchAndNothingElse()
    {
        using var collector = new MetricCollector<long>(
            _shared.Services.GetRequiredService<IMeterFactory>(),
            BuildInfoMetrics.MeterName,
            CspReportMetrics.CounterName);
        using var client = _shared.CreateClient();

        using var response = await PostAsync(client, Batch, ReportingType);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var window = Drain(_shared);
        Assert.Equal(2, window.Violations.Count);
        Assert.Equal(1, window.Violations[new("script-src-elem", "enforce")]);
        Assert.Equal(1, window.Violations[new("require-trusted-types-for", "report")]);
        Assert.Equal(2, collector.GetMeasurementSnapshot().Count);
    }

    [Fact]
    public async Task ReadsTheLegacyFormatByItsViolatedDirective()
    {
        using var client = _shared.CreateClient();

        using var response = await PostAsync(client, Legacy, LegacyType);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var violation = Assert.Single(Drain(_shared).Violations);
        Assert.Equal(new CspViolation("img-src", "unknown"), violation.Key);
    }

    [Theory]
    [InlineData("text/plain", Legacy, HttpStatusCode.UnsupportedMediaType)]
    [InlineData(ReportingType, "[{\"type\": ", HttpStatusCode.BadRequest)]
    // Well-formed, but its string is half a surrogate pair: found only as it is read.
    [InlineData(LegacyType, "{\"csp-report\": {\"violated-directive\": \"\\uD800\"}}",
        HttpStatusCode.BadRequest)]
    public async Task RefusesWhatIsNotAReport(string type, string body, HttpStatusCode status)
    {
        using var client = _shared.CreateClient();

        using var response = await PostAsync(client, body, type);

        Assert.Equal(status, response.StatusCode);
        var window = Drain(_shared);
        Assert.Empty(window.Violations);
        Assert.Equal(1, Assert.Single(window.Refusals).Value);
    }

    // A body that states its length is judged by it, unread; a streamed one (chunked, no
    // Content-Length) is read one byte past the bound at most.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefusesAndCountsABodyPastItsBound(bool statesLength)
    {
        await using var factory = Bounded("MaxBodyBytes", Bytes(Legacy, -1));
        var host = Receiving(factory, statesLength);
        using var client = host.CreateClient();

        using var response = await PostAsync(client, Legacy, LegacyType);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        var refusal = Assert.Single(Drain(host).Refusals);
        Assert.Equal(CspReportRefusal.Oversized, refusal.Key);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadsABodyOfExactlyItsBound(bool statesLength)
    {
        await using var factory = Bounded("MaxBodyBytes", Bytes(Legacy, 0));
        var host = Receiving(factory, statesLength);
        using var client = host.CreateClient();

        using var response = await PostAsync(client, Legacy, LegacyType);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(Drain(host).Violations);
    }

    [Fact]
    public async Task RefusesAReportFromAnotherSite()
    {
        using var client = _shared.CreateClient();

        using var response = await PostAsync(client, Legacy, LegacyType, "https://evil.test");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(Drain(_shared).IsEmpty);
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

    // The test server always states the length of a body: a streamed one loses it on the way in.
    // The derived host is disposed with its factory.
    private static WebApplicationFactory<Program> Receiving(
        ApiFactory factory,
        bool statesLength) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            if (!statesLength)
            {
                services.AddSingleton<IStartupFilter, StreamedBodies>();
            }
        }));

    private static CspReportWindow Drain(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<CspReportTally>().Drain();

    // The UTF-8 size of a body, shifted by delta, as a setting.
    private static string Bytes(string body, int delta) =>
        (Encoding.UTF8.GetByteCount(body) + delta).ToString(CultureInfo.InvariantCulture);

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

    /// <summary>Every request body arrives without its length, as a chunked one does.</summary>
    private sealed class StreamedBodies : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    context.Request.ContentLength = null;
                    return nextMiddleware(context);
                });
                next(app);
            };
    }
}
