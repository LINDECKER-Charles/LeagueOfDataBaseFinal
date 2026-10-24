using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using LoDb.Api.Modules.Analytics.Capture;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Robots.Capture;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Analytics;

/// <summary>
/// The robots journal through the API: nginx's reports taken on the internal route, the
/// robots, the detail of one and the journal read by the administrators alone, every filter
/// checked.
/// </summary>
/// <remarks>
/// No address is looked up (<c>DnsLookupsPerBatch</c> 0): the tests never reach the network.
/// </remarks>
public sealed class RobotsAdminTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private const string Journal = "/internal/robots/journal";
    private const string Robots = "/api/admin/analytics/robots";
    private const string Hits = "/api/admin/analytics/robot-hits";
    private const string Breakdown = "/api/admin/analytics/breakdown";
    private const string Googlebot =
        "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheJournalIsTakenThenReported()
    {
        using var taken = await PostJournalAsync(
            Line(Googlebot, "/fr/16.1.1/items", 200),
            Line(Googlebot, "/sitemap.xml", 503),
            Line("Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Firefox/140.0", "/fr/items", 200));
        await App.Services.GetRequiredService<IRobotJournalPump>().FlushAsync(Cancellation);
        using var admin = await AdminBrowser.OpenAsync(App);

        using var report = await admin.GetAsync(Robots + "?range=7d");
        using var detail = await admin.GetAsync(Robots + "/googlebot");
        using var hits = await admin.GetAsync(Hits + "?robot=googlebot&status=5");

        Assert.Equal(HttpStatusCode.Accepted, taken.StatusCode);
        var robots = await ApiJson.ReadAsync(report, HttpStatusCode.OK);
        var first = robots.GetProperty("robots")[0];
        Assert.Equal("googlebot", ApiJson.Text(first, "key"));
        Assert.Equal(2, first.GetProperty("figures").GetProperty("hits").GetInt32());
        var robot = (await ApiJson.ReadAsync(detail, HttpStatusCode.OK)).GetProperty("robot");
        Assert.Equal("Google", ApiJson.Text(robot, "operator"));
        var page = await ApiJson.ReadAsync(hits, HttpStatusCode.OK);
        Assert.Equal("/sitemap.xml", ApiJson.Text(page.GetProperty("items")[0], "target"));
        Assert.Equal("no-store", report.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task AnUnknownRobotIsNotFound()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(Robots + "/nobody");

        Assert.Equal(
            "robot-not-found",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task AJournalPastItsBoundIsRefused()
    {
        using var response = await PostAsync(new string('x', JournalBody.MaxBytes + 1));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Theory]
    [InlineData(Robots + "?range=5d", "range", "invalid-range")]
    [InlineData(Hits + "?category=aliens", "category", "invalid-filter")]
    [InlineData(Hits + "?status=9", "status", "invalid-filter")]
    [InlineData(Breakdown + "?dimension=colors", "dimension", "invalid-dimension")]
    public async Task UnknownFiltersAreRefused(string url, string field, string code)
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(url);

        Assert.Equal([code], (await ApiJson.FieldErrorsAsync(response))[field]);
    }

    [Theory]
    [InlineData(Robots)]
    [InlineData(Hits)]
    [InlineData(Breakdown)]
    public async Task TheReportsBelongToTheAdministrators(string url)
    {
        using var member = await AdminBrowser.OpenMemberAsync(App);
        using var anonymous = App.Browser();

        using var memberResponse = await member.GetAsync(url);
        using var anonymousResponse = await anonymous.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, memberResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(
        postgres,
        new Dictionary<string, string?> { ["LoDb:Robots:DnsLookupsPerBatch"] = "0" },
        static _ => { });

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private Task<HttpResponseMessage> PostJournalAsync(params string[] lines) =>
        PostAsync(string.Join('\n', lines));

    // As nginx posts it: from the stack's network, without a session.
    private async Task<HttpResponseMessage> PostAsync(string body)
    {
        using var client = App.App();
        using var content = new StringContent(body, Encoding.UTF8, "application/x-ndjson");
        return await client.PostAsync(new Uri(Journal, UriKind.Relative), content, Cancellation);
    }

    private string Line(string agent, string target, int status) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["t"] = (App.Clock.GetUtcNow().ToUnixTimeMilliseconds() / 1000d)
                .ToString(CultureInfo.InvariantCulture),
            ["ip"] = "192.0.2.4",
            ["m"] = "GET",
            ["u"] = target,
            ["s"] = status,
            ["d"] = 0.05,
            ["b"] = 1024,
            ["a"] = agent,
            ["c"] = "MISS",
        });
}
