namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// The catalog rows of the 301 table (plan-migration.md): lists and details, with and without
/// <c>?lang=</c> and version. 16.19.1 is the latest recorded version, 16.18.1 the one before.
/// </summary>
/// <remarks>
/// The forms of the old paths are LegacyPathParserTests', the choice of locale and version
/// LegacyTargetTests': these rows prove the endpoint wires them, and look each kind of entry
/// up in the catalog.
/// </remarks>
[Collection(LegacyApiGroup.Name)]
public sealed class LegacyCatalogRedirectTests(LegacyApiFixture api)
{
    // The version in the path, then in the query with the language.
    [Theory]
    [InlineData("/16.18.1/champions", "/en/16.18.1/champions")]
    [InlineData("/objects?version=16.18.1&lang=fr_FR", "/fr/16.18.1/items")]
    public async Task ListLandsOnItsLocaleAndVersion(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    // One row per resource: each one looks its old name up its own way.
    [Theory]
    [InlineData("/champion/MonkeyKing?lang=fr_FR", "/fr/champions/MonkeyKing")]
    [InlineData("/object/1004", "/en/items/1004-faerie-charm")]
    [InlineData("/rune/Domination", "/en/runes/8100-domination")]
    [InlineData("/summoner/SummonerFlash_Jade", "/en/summoners/SummonerFlash_Jade")]
    public async Task DetailLandsOnTheCanonicalPath(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    [Theory]
    [InlineData("/16.18.1/object/1004?lang=fr_FR", "/fr/16.18.1/items/1004-faerie-charm")]
    [InlineData("/champion/Teemo?version=16.18.1", "/en/16.18.1/champions/Teemo")]
    public async Task DetailOfAPastVersionKeepsItsVersion(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    [Fact]
    public async Task LatestVersionLandsOnTheShortUrlInOneHop() =>
        Assert.Equal("/en/champions/Ahri", await api.LocationOfAsync("/16.19.1/champion/Ahri"));

    [Theory]
    [InlineData("/champions?version=nope", "/en/champions")]
    [InlineData("/champion/Ahri?lang=english", "/en/champions/Ahri")]
    public async Task InvalidQueryIsIgnoredAsTheOldSiteDid(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));
}
