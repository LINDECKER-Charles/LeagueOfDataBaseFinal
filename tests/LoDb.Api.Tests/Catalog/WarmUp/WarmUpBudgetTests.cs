using System.Net;
using System.Text.Json;
using LoDb.Testing;

namespace LoDb.Api.Tests.Catalog.WarmUp;

/// <summary>
/// A warm-up under the visitors' budgets of cold work: images refused end the run done, the
/// catalog open and the images left pending for the pages; a catalog refused still ends it
/// failed.
/// </summary>
public sealed class WarmUpBudgetTests(PostgresContainerFixture postgres)
{
    private const string Cold = "/api/catalog/16.18.1/en_US";
    private const string ChampionsThenSummoners = "resources=champions&resources=summoners";
    private const string SpellIcons = "/16.18.1/img/spell/";
    private const string ImageBudgetReason = "image_budget";

    // The recording has five champions: a budget of two leaves three portraits refused.
    private const int RecordedChampions = 5;
    private const int ImageBudget = 2;

    [Fact]
    public async Task RefusedImagesEndTheRunDoneAndStayPending()
    {
        await using var api = await WarmUpBudgetApi.StartAsync(
            postgres,
            ("ColdImagesPerMinute", ImageBudget));

        var frames = await api.WarmUpAsync($"{Cold}/warm-up?{ChampionsThenSummoners}");

        Assert.Equal("preparing", frames[0].Text("stage"));
        var done = frames[^1];
        Assert.Equal("done", done.Text("stage"));
        Assert.True(done.IsNull("failure"));
        var (champions, summoners) = (done.Items("resources")[0], done.Items("resources")[1]);
        Assert.Equal((RecordedChampions, ImageBudget, false), CountsOf(champions));
        Assert.True(summoners.GetProperty("total").GetInt32() > 0);
        Assert.Equal((0, false), (CountsOf(summoners).Settled, CountsOf(summoners).Ready));
        Assert.Equal(ImageBudget, done.GetProperty("settled").GetInt32());
        Assert.DoesNotContain(
            api.UpstreamRequests,
            static url => url.AbsolutePath.Contains(SpellIcons, StringComparison.Ordinal));
        var refusal = Assert.Single(api.Refusals.GetMeasurementSnapshot());
        Assert.Equal(ImageBudgetReason, refusal.Tags["reason"]);
    }

    [Fact]
    public async Task ImagesLeftByARefusedRunArePendingNotAbsent()
    {
        await using var api = await WarmUpBudgetApi.StartAsync(
            postgres,
            ("ColdImagesPerMinute", ImageBudget));
        await api.WarmUpAsync($"{Cold}/warm-up?resources=champions");

        using var response = await api.GetAsync($"{Cold}/champions");
        using var list = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal(
            ["pending", "pending", "pending", "present", "present"],
            list.RootElement.Items("entries")
                .Select(static entry => entry.GetProperty("image").Text("status"))
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task RefusedCatalogStillEndsTheRunFailed()
    {
        await using var api = await WarmUpBudgetApi.StartAsync(
            postgres,
            ("ColdDatasetsPerMinute", 0));

        var last = (await api.WarmUpAsync($"{Cold}/warm-up?{ChampionsThenSummoners}"))[^1];

        Assert.Equal(("failed", "catalog-pending"), (last.Text("stage"), last.Text("failure")));
        Assert.Equal(0, last.GetProperty("total").GetInt32());
        Assert.DoesNotContain(
            api.UpstreamRequests,
            static url => url.AbsolutePath.Contains("/16.18.1/", StringComparison.Ordinal));
    }

    private static (int Total, int Settled, bool Ready) CountsOf(JsonElement resource) =>
        (resource.GetProperty("total").GetInt32(),
            resource.GetProperty("settled").GetInt32(),
            resource.GetProperty("ready").GetBoolean());
}
