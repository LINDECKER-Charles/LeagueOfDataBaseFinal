using System.Net;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The detail pages: the card, the derived facts and the hotlinks, every image resolved
/// before the answer; and the typed failures the routing decides on.
/// </summary>
[Collection(CatalogApiGroup.Name)]
public sealed class CatalogDetailTests(CatalogApiFixture api)
{
    private const string Latest = "/api/catalog/16.19.1/en_US";

    [Fact]
    public async Task ChampionDetailCarriesAbilitiesSkinsAndArt()
    {
        var ahri = await api.GetJsonAsync($"{Latest}/champions/Ahri");

        Assert.Equal("champions/Ahri", ahri.Text("canonicalPath"));
        Assert.Equal("Ahri", ahri.GetProperty("profile").Text("name"));
        Assert.Equal(
            ["passive", "q", "w", "e", "r"],
            ahri.Pluck("abilities", "slot"));
        Assert.All(ahri.Items("abilities"), static ability =>
            Assert.Equal("present", ability.GetProperty("image").Text("status")));
        Assert.Equal(
            "https://ddragon.leagueoflegends.com/cdn/img/champion/splash/Ahri_0.jpg",
            ahri.GetProperty("art").Text("splash"));
        Assert.NotEmpty(ahri.Items("skins"));
    }

    [Fact]
    public async Task ColdVersionDetailResolvesItsImagesBeforeAnswering()
    {
        using var response = await api.GetAsync("/api/catalog/16.18.1/en_US/champions/Garen");
        var body = await response.Content.ReadAsStringAsync(CatalogApiFixture.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.RetryAfter);
        Assert.Contains("\"status\":\"present\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"status\":\"pending\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ItemDetailCarriesItsTwinAndMaps()
    {
        var charm = await api.GetJsonAsync($"{Latest}/items/1004-faerie-charm");
        var classic = await api.GetJsonAsync($"{Latest}/items/771004");

        Assert.Equal("items/1004-faerie-charm", charm.Text("canonicalPath"));
        var profile = charm.GetProperty("profile");
        Assert.Equal(("modern", "771004"),
            (profile.Text("edition"), profile.GetProperty("counterpart").Text("id")));
        Assert.Equal("present", profile.GetProperty("image").Text("status"));
        Assert.DoesNotContain(453, charm.GetProperty("availableMaps").EnumerateArray()
            .Select(static map => map.GetInt32()));
        Assert.Equal("classic", classic.GetProperty("profile").Text("edition"));
        Assert.Equal("1004", classic.GetProperty("profile").GetProperty("counterpart").Text("id"));
    }

    [Fact]
    public async Task ItemDetailCarriesItsRecipe()
    {
        var trinity = await api.GetJsonAsync($"{Latest}/items/3078");

        var recipe = trinity.GetProperty("recipe");
        Assert.Equal(("3078", "items/3078-trinity-force"),
            (recipe.Text("id"), recipe.Text("canonicalPath")));
        Assert.Equal("present", recipe.GetProperty("image").Text("status"));
    }

    [Fact]
    public async Task ItemDetailPricesItsUpgradesAndGivesItsDepth()
    {
        var boots = await api.GetJsonAsync($"{Latest}/items/1001");
        var greaves = await api.GetJsonAsync($"{Latest}/items/3006");

        var upgrade = Assert.Single(boots.Items("upgrades"));
        Assert.Equal(
            ("3006", 1100, "modern"),
            (upgrade.Text("id"), upgrade.GetProperty("gold").GetInt32(), upgrade.Text("edition")));
        Assert.True(boots.IsNull("depth"));
        Assert.Equal(2, greaves.GetProperty("depth").GetInt32());
    }

    [Fact]
    public async Task SummonerDetailCarriesItsEditionAndRange()
    {
        var jade = await api.GetJsonAsync($"{Latest}/summoners/SummonerFlash_Jade");

        var profile = jade.GetProperty("profile");
        Assert.Equal(("classic", "SummonerFlash"),
            (profile.Text("edition"), profile.GetProperty("counterpart").Text("id")));
        Assert.Equal("summoners/SummonerFlash_Jade", jade.Text("canonicalPath"));
        Assert.False(jade.GetProperty("globalRange").GetBoolean());
    }

    [Theory]
    [InlineData("8100")]
    [InlineData("8100-domination")]
    [InlineData("8100-stale-slug")]
    public async Task RuneTreeIsFoundWhateverItsSlug(string segment)
    {
        var tree = await api.GetJsonAsync($"{Latest}/runes/{segment}");

        Assert.Equal("runes/8100-domination", tree.Text("canonicalPath"));
        Assert.Equal(4, tree.Items("slots").Count);
    }

    [Theory]
    [InlineData("champions/Nobody")]
    [InlineData("items/999999")]
    [InlineData("items/2008")]
    [InlineData("items/not-a-number")]
    [InlineData("runes/1")]
    [InlineData("summoners/SummonerNothing")]
    public async Task UnknownEntityIsNotFound(string path)
    {
        var problem = await api.GetProblemAsync($"{Latest}/{path}", HttpStatusCode.NotFound);

        Assert.Equal("unknown-entity", problem.Text("code"));
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("/api/catalog/99.1.1/en_US/items/1004", "unknown-version")]
    [InlineData("/api/catalog/99.1.1/en_US/champions", "unknown-version")]
    [InlineData("/api/catalog/16.19.1/de_DE/items/1004", "unknown-language")]
    public async Task UnlistedCatalogIsNotFound(string path, string code)
    {
        var problem = await api.GetProblemAsync(path, HttpStatusCode.NotFound);

        Assert.Equal(code, problem.Text("code"));
    }

    [Theory]
    [InlineData("/api/catalog/abc/en_US/items/1004", "invalid-version")]
    [InlineData("/api/catalog/v16.19.1/en_US/champions", "invalid-version")]
    [InlineData("/api/catalog/16.19.1/english/champions/Ahri", "invalid-language")]
    public async Task MalformedCatalogIsABadRequest(string path, string code)
    {
        var problem = await api.GetProblemAsync(path, HttpStatusCode.BadRequest);

        Assert.Equal(code, problem.Text("code"));
    }
}
