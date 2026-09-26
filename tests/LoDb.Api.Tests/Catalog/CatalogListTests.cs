using System.Net;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The four lists: every card with its canonical path and facets, the Classic twins among
/// the items and the summoner spells, pages for the server-side rendering, and a cold
/// version's placeholders.
/// </summary>
[Collection(CatalogApiGroup.Name)]
public sealed class CatalogListTests(CatalogApiFixture api)
{
    private const string Latest = "/api/catalog/16.19.1/en_US";

    [Fact]
    public async Task ChampionListCarriesCardsAndFacets()
    {
        var list = await api.GetJsonAsync($"{Latest}/champions");

        Assert.Equal(("16.19.1", "en_US", 5), (list.Text("version"), list.Text("language"),
            list.GetProperty("total").GetInt32()));
        Assert.True(list.IsNull("page"));
        var wukong = list.Entry("entries", "MonkeyKing");
        Assert.Equal("Wukong", wukong.Text("name"));
        Assert.Equal("champions/MonkeyKing", wukong.Text("canonicalPath"));
        Assert.Equal("present", wukong.GetProperty("image").Text("status"));
        Assert.StartsWith(
            "/cdn/blobs/",
            wukong.GetProperty("image").Text("url"),
            StringComparison.Ordinal);
        Assert.Contains("health", wukong.Items("stats").Select(static stat => stat.Text("stat")));
        Assert.NotEmpty(list.GetProperty("facets").Texts("tags"));
    }

    [Fact]
    public async Task ItemListHoldsBothEditionsAndLeavesDebrisOut()
    {
        var list = await api.GetJsonAsync($"{Latest}/items");

        var ids = list.Pluck("entries", "id");
        Assert.Contains("1004", ids);
        Assert.Contains("771004", ids);
        Assert.DoesNotContain("2008", ids);
        Assert.DoesNotContain("226660", ids);
        var modern = list.Entry("entries", "1004");
        var classic = list.Entry("entries", "771004");
        Assert.Equal("items/1004-faerie-charm", modern.Text("canonicalPath"));
        Assert.Equal(("modern", "classic"), (modern.Text("edition"), classic.Text("edition")));
        Assert.Equal(
            ("771004", "classic", classic.Text("canonicalPath")),
            (modern.GetProperty("counterpart").Text("id"),
                modern.GetProperty("counterpart").Text("edition"),
                modern.GetProperty("counterpart").Text("canonicalPath")));
        Assert.Equal("1004", classic.GetProperty("counterpart").Text("id"));
    }

    [Fact]
    public async Task SummonerListHoldsTheClassicTwins()
    {
        var list = await api.GetJsonAsync($"{Latest}/summoners");

        var flash = list.Entry("entries", "SummonerFlash");
        var jade = list.Entry("entries", "SummonerFlash_Jade");
        Assert.Equal(("modern", "classic"), (flash.Text("edition"), jade.Text("edition")));
        Assert.Equal("SummonerFlash_Jade", flash.GetProperty("counterpart").Text("id"));
        Assert.Equal("summoners/SummonerFlash_Jade", jade.Text("canonicalPath"));
        Assert.Equal("SummonerFlash", jade.GetProperty("counterpart").Text("id"));
        var modes = flash.Pluck("modes", "code");
        Assert.Contains("CLASSIC", modes);
        Assert.DoesNotContain("RUBY_TRIAL_1", modes);
        Assert.Contains("CLASSIC", list.GetProperty("facets").Texts("modes"));
    }

    [Fact]
    public async Task RuneListCarriesTreesAndRunes()
    {
        var list = await api.GetJsonAsync($"{Latest}/runes");

        Assert.Equal(["8100", "8000"], list.Pluck("trees", "id"));
        Assert.Equal("runes/8100-domination", list.Entry("trees", "8100").Text("canonicalPath"));
        var electrocute = list.Items("entries").Single(static rune =>
            rune.Text("key") == "Electrocute");
        Assert.Equal(("Domination", "runes/8100-domination"),
            (electrocute.Text("tree"), electrocute.Text("canonicalPath")));
    }

    [Fact]
    public async Task RunesAreEmptyBefore7221()
    {
        var list = await api.GetJsonAsync("/api/catalog/7.21.1/en_US/runes");

        Assert.Equal(0, list.GetProperty("total").GetInt32());
        Assert.Empty(list.Items("trees"));
        Assert.Empty(list.Items("entries"));
    }

    [Fact]
    public async Task PageSlicesTheListButKeepsItsTotal()
    {
        var first = await api.GetJsonAsync($"{Latest}/champions?page=1&size=2");
        var last = await api.GetJsonAsync($"{Latest}/champions?page=3&size=2");
        var beyond = await api.GetJsonAsync($"{Latest}/champions?page=4&size=2");

        Assert.Equal((1, 2, 5), (first.GetProperty("page").GetInt32(),
            first.GetProperty("size").GetInt32(), first.GetProperty("total").GetInt32()));
        Assert.Equal(2, first.Items("entries").Count);
        Assert.Single(last.Items("entries"));
        Assert.Empty(beyond.Items("entries"));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("size=0")]
    [InlineData("page=1&size=201")]
    public async Task PageOutOfRangeIsRefused(string query)
    {
        var problem = await api.GetProblemAsync(
            $"{Latest}/items?{query}",
            HttpStatusCode.BadRequest);

        Assert.Equal("invalid-page", problem.Text("code"));
    }

    [Fact]
    public async Task ColdVersionListShowsPendingPlaceholders()
    {
        using var response = await api.GetAsync("/api/catalog/8.7.1/en_US/champions");
        var list = await response.Content.ReadAsStringAsync(CatalogApiFixture.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), response.Headers.RetryAfter?.Delta);
        Assert.Equal("public, max-age=5", CacheHeaderTests.CacheControlOf(response));
        Assert.Contains("\"status\":\"pending\"", list, StringComparison.Ordinal);
        Assert.DoesNotContain("\"status\":\"present\"", list, StringComparison.Ordinal);
    }
}
