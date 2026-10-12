using System.Net;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The pickers of the build editor and the profile: only what a build may carry, by name,
/// images resolved.
/// </summary>
[Collection(CatalogApiGroup.Name)]
public sealed class PickerEndpointTests(CatalogApiFixture api)
{
    private const string Scope = "version=16.19.1&lang=en_US";

    [Fact]
    public async Task ChampionsAreSortedByName()
    {
        var picker = await api.GetJsonAsync($"/api/pickers/champions?{Scope}");

        Assert.Equal(
            ["Ahri", "Fiddlesticks", "Garen", "Teemo", "Wukong"],
            picker.Pluck("options", "name"));
        Assert.All(picker.Items("options"), static option =>
            Assert.Equal("present", option.GetProperty("image").Text("status")));
    }

    // Which items a build may carry is ItemPlayability's (Domain): the exact list proves the
    // picker applies it, the arena one that it applies it to the mode asked.
    [Fact]
    public async Task ItemsOfTheModeLeaveTheUnbuyableOut()
    {
        var rift = await api.GetJsonAsync($"/api/pickers/items?{Scope}&mode=sr");
        var arena = await api.GetJsonAsync($"/api/pickers/items?{Scope}&mode=arena");

        Assert.Equal("sr", rift.Text("mode"));
        Assert.Equal(
            ["1001", "1004", "1042", "2003", "3006", "3078"],
            rift.Pluck("options", "id").Order(StringComparer.Ordinal));
        Assert.Equal(300, rift.Entry("options", "1001").GetProperty("gold").GetInt32());
        Assert.Equal("arena", arena.Text("mode"));
        Assert.DoesNotContain("1001", arena.Pluck("options", "id"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("&mode=urf")]
    public async Task UnknownModeFallsBackToSummonersRift(string mode)
    {
        var picker = await api.GetJsonAsync($"/api/pickers/items?{Scope}{mode}");

        Assert.Equal("sr", picker.Text("mode"));
    }

    [Fact]
    public async Task RunesComeTreeBySlot()
    {
        var picker = await api.GetJsonAsync($"/api/pickers/runes?{Scope}");

        var domination = picker.Entry("trees", "8100");
        var keystones = domination.GetProperty("slots")[0];
        Assert.Equal("Electrocute", keystones[0].Text("key"));
        Assert.Equal(4, domination.GetProperty("slots").GetArrayLength());
    }

    [Fact]
    public async Task SummonersAreThoseOfSummonersRift()
    {
        var picker = await api.GetJsonAsync($"/api/pickers/summoners?{Scope}");

        Assert.Equal(
            ["SummonerExhaust", "SummonerFlash", "SummonerHeal"],
            picker.Pluck("options", "id"));
    }

    [Fact]
    public async Task SkinsOfAChampionAreHotlinked()
    {
        var picker = await api.GetJsonAsync($"/api/pickers/skins?{Scope}&champion=Ahri");

        Assert.Equal("Ahri", picker.Text("champion"));
        var skin = picker.Entry("skins", "Ahri_0");
        Assert.Equal(0, skin.GetProperty("number").GetInt32());
        Assert.EndsWith("/loading/Ahri_0.jpg", skin.Text("image"), StringComparison.Ordinal);
        Assert.EndsWith("/centered/Ahri_0.jpg", skin.Text("banner"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("&champion=Nobody")]
    public async Task UnknownChampionHasNoSkins(string champion)
    {
        var picker = await api.GetJsonAsync($"/api/pickers/skins?{Scope}{champion}");

        Assert.True(picker.IsNull("champion"));
        Assert.Empty(picker.Items("skins"));
    }

    [Theory]
    [InlineData("champions?lang=en_US", HttpStatusCode.BadRequest, "invalid-version")]
    [InlineData("runes?version=16.19.1", HttpStatusCode.BadRequest, "invalid-language")]
    [InlineData("items?version=99.1.1&lang=en_US", HttpStatusCode.NotFound, "unknown-version")]
    public async Task PickerNeedsAKnownCatalog(string query, HttpStatusCode status, string code)
    {
        var problem = await api.GetProblemAsync($"/api/pickers/{query}", status);

        Assert.Equal(code, problem.Text("code"));
    }
}
