using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Analytics.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Analytics;
using LoDb.Infrastructure.Persistence.Analytics.Partitions;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Analytics;

/// <summary>
/// The back office's analytics: the report and the rollup belong to the administrators, and
/// the beacon of a signed-in reader needs no XSRF token.
/// </summary>
public sealed class AnalyticsAdminTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string Report = "/api/admin/analytics/report";
    private const string Rollup = "/api/admin/analytics/rollup";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RollupFoldsTodayAndIsAudited()
    {
        await WriteViewsAsync("/fr/items", "/fr/champions/Ahri");
        using var admin = await AdminBrowser.OpenAsync(App);

        using var rolled = await admin.PostAsync(Rollup);
        using var reported = await admin.GetAsync(Report + "?range=7d");

        var receipt = await ApiJson.ReadAsync(rolled, HttpStatusCode.OK);
        var today = DateOnly.FromDateTime(App.Clock.GetUtcNow().UtcDateTime);
        Assert.Equal(
            [today.ToString("O", CultureInfo.InvariantCulture)],
            receipt.GetProperty("days").EnumerateArray().Select(static day => day.GetString()));
        var report = await ApiJson.ReadAsync(reported, HttpStatusCode.OK);
        Assert.Equal(("7d", 7), (ApiJson.Text(report, "range"), Number(report, "days")));
        Assert.Equal(2, Number(report.GetProperty("totals"), "views"));
        Assert.Equal("no-store", reported.Headers.CacheControl?.ToString());
        var entry = Assert.Single(
            await App.AuditAsync(),
            static entry => entry.Action == AuditAction.AdminAnalyticsRollup);
        using var meta = JsonDocument.Parse(entry.Meta ?? "{}");
        Assert.Equal(1, meta.RootElement.GetProperty("rolled").GetInt32());
    }

    [Theory]
    [InlineData(null, "30d", 30)]
    [InlineData("90d", "90d", 90)]
    [InlineData("all", "all", 1)]
    public async Task ReportCoversTheRange(string? range, string name, int days)
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(
            range is null ? Report : $"{Report}?range={range}");

        var report = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal(name, ApiJson.Text(report, "range"));
        Assert.Equal(days, Number(report, "days"));
    }

    [Fact]
    public async Task UnknownRangeIsRefused()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(Report + "?range=5d");

        Assert.Equal(["invalid-range"], (await ApiJson.FieldErrorsAsync(response))["range"]);
    }

    [Fact]
    public async Task ReportAndRollupBelongToTheAdministrators()
    {
        using var member = await AdminBrowser.OpenMemberAsync(App);
        using var anonymous = App.Browser();

        using var memberReport = await member.GetAsync(Report);
        using var memberRollup = await member.PostAsync(Rollup);
        using var anonymousReport = await anonymous.GetAsync(Report);

        Assert.Equal(HttpStatusCode.Forbidden, memberReport.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, memberRollup.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousReport.StatusCode);
    }

    [Fact]
    public async Task SignedInReaderSendsTheBeaconWithoutAToken()
    {
        using var member = await AdminBrowser.OpenMemberAsync(App);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(AnalyticsApiFixture.BeaconPath, UriKind.Relative))
        {
            Content = JsonContent.Create(new { path = "/fr/items" }),
        };
        request.Headers.Add(BrowserClient.OriginHeader, AccountsApp.SiteOrigin);

        using var response = await member.SendAsync(request);

        Assert.NotNull(member.Cookie(BrowserClient.SessionCookie));
        Assert.False(request.Headers.Contains(BrowserClient.XsrfHeader));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static int Number(JsonElement element, string name) =>
        element.GetProperty(name).GetInt32();

    // Written as the writer would, the resolver aside: this host has no Data Dragon.
    private async Task WriteViewsAsync(params string[] paths)
    {
        var now = App.Clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var partitions = App.Services.GetRequiredService<IAnalyticsPartitions>();
        await partitions.CreateAsync(today, today, Cancellation);
        var views = paths.Select(path => new AnalyticsEvent
        {
            OccurredAt = now,
            Origin = AnalyticsCaptureOrigin.Navigation,
            Route = "app_items",
            Path = path,
            Type = "item",
            Kind = "list",
            Status = 200,
            Lang = "fr_FR",
            Locale = "fr",
            Visitor = "0123456789abcdef",
            Browser = "Chrome",
            Os = "Windows",
            Device = "desktop",
            RefererSource = "direct",
        }).ToList();
        await App.Services.GetRequiredService<IAnalyticsEventWriter>()
            .WriteAsync(views, Cancellation);
    }
}
