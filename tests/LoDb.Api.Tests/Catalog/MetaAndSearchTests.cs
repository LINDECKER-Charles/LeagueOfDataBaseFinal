using System.Net;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The lists the front reads instead of copying them, and the accent-insensitive search.
/// </summary>
[Collection(CatalogApiGroup.Name)]
public sealed class MetaAndSearchTests(CatalogApiFixture api)
{
    private const string French = "/api/catalog/16.19.1/fr_FR";

    [Fact]
    public async Task MetaListsVersionsLanguagesLocalesAndModes()
    {
        var meta = await api.GetJsonAsync("/api/meta");

        Assert.Equal("16.19.1", meta.Text("latest"));
        Assert.Contains("16.18.1", meta.Texts("versions"));
        Assert.Contains("16.19.1", meta.Texts("readyVersions"));
        Assert.Equal(@"\d+(?:\.\d+)+", meta.Text("versionPattern"));
        Assert.Contains("fr_FR", meta.Texts("languages"));
        Assert.Equal("en_US", meta.Text("defaultLanguage"));
        var locales = meta.Items("locales");
        Assert.Contains(locales, static locale =>
            locale.Text("locale") == "fr" && locale.Text("language") == "fr_FR");
        Assert.Equal(["sr", "aram", "nexus_blitz", "arena"], meta.Pluck("gameModes", "mode"));
        Assert.Equal("sr", meta.Text("defaultGameMode"));
    }

    [Theory]
    [InlineData("feerique")]
    [InlineData("FÉÉRIQUE")]
    public async Task SearchIgnoresAccentsAndCase(string query)
    {
        var results = await api.GetJsonAsync($"{French}/search?q={Uri.EscapeDataString(query)}");

        Assert.Equal(query, results.Text("query"));
        Assert.Equal(["1004", "771004"], results.Pluck("hits", "id"));
        var classic = results.Entry("hits", "771004");
        Assert.Equal(("items", "classic", "items/771004-faerie-charm"),
            (classic.Text("type"), classic.Text("edition"), classic.Text("canonicalPath")));
        Assert.Equal("present", classic.GetProperty("image").Text("status"));
    }

    [Fact]
    public async Task SearchFiltersOnTypes()
    {
        var all = await api.GetJsonAsync($"{French}/search?q=flash");
        var champions = await api.GetJsonAsync($"{French}/search?q=ahri&types=champions,items");
        var none = await api.GetJsonAsync($"{French}/search?q=flash&types=items");

        Assert.Equal(["SummonerFlash", "SummonerFlash_Jade"], all.Pluck("hits", "id"));
        Assert.Equal(["champions"], champions.Pluck("hits", "type"));
        Assert.Empty(none.Items("hits"));
    }

    [Theory]
    [InlineData("search")]
    [InlineData("search?q=a")]
    [InlineData("search?q=flash&types=skins")]
    public async Task InvalidSearchIsABadRequest(string query)
    {
        var problem = await api.GetProblemAsync($"{French}/{query}", HttpStatusCode.BadRequest);

        Assert.Equal("invalid-query", problem.Text("code"));
    }
}
