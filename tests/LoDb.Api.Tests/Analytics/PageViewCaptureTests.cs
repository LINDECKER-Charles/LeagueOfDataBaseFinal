using System.Net;
using System.Text;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Analytics.Support;
using LoDb.Infrastructure.Analytics.Capture;
using LoDb.Infrastructure.Analytics.Reports;
using LoDb.Infrastructure.Analytics.Rollup;
using LoDb.Infrastructure.Persistence.Analytics;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Analytics;

/// <summary>
/// A visit as the site sees it: nginx mirrors the page the reader arrives on, the router's
/// beacon reports the two pages it navigates to, and the report counts three views.
/// </summary>
/// <remarks>
/// The only tests of the collection that write views: the others resolve pages or are
/// refused before the queue.
/// </remarks>
[Collection(AnalyticsApiGroup.Name)]
public sealed class PageViewCaptureTests(AnalyticsApiFixture api)
{
    private const string ForeignOrigin = "https://evil.example";

    [Fact]
    public async Task ArrivalAndTwoNavigationsMakeThreeViews()
    {
        using var arrival = await api.MirrorAsync("/fr/champions/Ahri?lang=fr_FR");
        using var list = await api.BeaconAsync(new { path = "/fr/items" });
        using var detail = await api.BeaconAsync(
            new { path = "/fr/runes/8100-domination" },
            AccountsApp.SiteOrigin);

        Assert.Equal(
            [HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.Accepted],
            [arrival.StatusCode, list.StatusCode, detail.StatusCode]);
        Assert.Equal(3, await Get<IPageViewPump>().FlushAsync(AnalyticsApiFixture.Token));
        var views = await api.EventsAsync();
        Assert.Equal(
            [
                (AnalyticsCaptureOrigin.ServedPage, "/fr/champions/Ahri", "Ahri"),
                (AnalyticsCaptureOrigin.Navigation, "/fr/items", null),
                (AnalyticsCaptureOrigin.Navigation, "/fr/runes/8100-domination", "Domination"),
            ],
            views.Select(static view => (view.Origin, view.Path, view.Entity)));
        Assert.All(views, static view => Assert.False(view.IsBot));
        Assert.Single(views.Select(static view => view.Visitor).Distinct());

        var report = await ReportAsync();
        Assert.Equal((3L, 1), (report.Totals.Views, report.Totals.UniqueVisitors));
        Assert.Equal(
            ["champion:Ahri", "runesReforged:Domination"],
            report.TopEntities.Select(static rank => rank.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fr/champions")]
    [InlineData("//evil.example/fr")]
    public async Task MirrorWithoutAPageAddressIsRefused(string? target)
    {
        using var response = await api.MirrorAsync(target);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BeaconFromAnotherSiteIsRefused()
    {
        using var response = await api.BeaconAsync(new { path = "/fr/items" }, ForeignOrigin);

        Assert.Equal(
            "origin-mismatch",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
    }

    [Theory]
    [InlineData("{\"path\":\"https://evil.example/fr\"}")]
    [InlineData("{\"path\":42}")]
    [InlineData("{\"route\":\"/fr/items\"}")]
    [InlineData("{not json")]
    [InlineData("")]
    public async Task BeaconWithoutAPageAddressIsRefused(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        using var response = await api.SendBeaconAsync(content, origin: null);

        Assert.Equal(["invalid-path"], (await ApiJson.FieldErrorsAsync(response))["path"]);
    }

    [Fact]
    public async Task OversizedBeaconIsRefused()
    {
        var body = "{\"path\":\"/fr/" + new string('a', 5000) + "\"}";
        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        using var response = await api.SendBeaconAsync(content, origin: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<AnalyticsReport> ReportAsync()
    {
        await Get<IAnalyticsRollup>().RollupTodayAsync(AnalyticsApiFixture.Token);
        Assert.True(AnalyticsRange.TryParse(AnalyticsRange.LastWeek, out var range));
        return await Get<IAnalyticsReports>().BuildAsync(range, AnalyticsApiFixture.Token);
    }

    private T Get<T>()
        where T : notnull =>
        api.Services.GetRequiredService<T>();
}
