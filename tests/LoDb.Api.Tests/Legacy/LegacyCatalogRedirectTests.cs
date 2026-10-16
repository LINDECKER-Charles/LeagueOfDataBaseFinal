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
    // The version in the path, then in the query with the language: a pinned list is
    // canonical to the latest bare one, where the redirect lands.
    [Theory]
    [InlineData("/16.18.1/champions", "/en/champions")]
    [InlineData("/objects?version=16.18.1&lang=fr_FR", "/fr/items")]
    public async Task ListOfAPastVersionLandsOnTheLatestList(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    // One row per resource: each one looks its old name up its own way.
    [Theory]
    [InlineData("/champion/MonkeyKing?lang=fr_FR", "/fr/champions/MonkeyKing")]
    [InlineData("/object/1004", "/en/items/1004-faerie-charm")]
    [InlineData("/rune/Domination", "/en/runes/8100-domination")]
    [InlineData("/summoner/SummonerFlash_Jade", "/en/summoners/SummonerFlash_Jade")]
    public async Task DetailLandsOnTheCanonicalPath(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    // An entry still in the game: its pinned page is canonical to the short URL, so the
    // redirect lands there, whatever version the old URL named.
    [Theory]
    [InlineData("/16.18.1/object/1004?lang=fr_FR", "/fr/items/1004-faerie-charm")]
    [InlineData("/champion/Teemo?version=16.18.1", "/en/champions/Teemo")]
    public async Task DetailOfAPastVersionLandsOnTheShortUrl(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    // 3001 was Abyssal Mask in 7.22.1, Evenshroud now: the latest slug, so no second hop
    // from a pinned slug to its own.
    [Fact]
    public async Task RenamedEntryLandsOnTheLatestSlugInOneHop() =>
        Assert.Equal("/en/items/3001-evenshroud", await api.LocationOfAsync("/7.22.1/object/3001"));

    [Fact]
    public async Task LatestVersionLandsOnTheShortUrlInOneHop() =>
        Assert.Equal("/en/champions/Ahri", await api.LocationOfAsync("/16.19.1/champion/Ahri"));

    [Theory]
    [InlineData("/champions?version=nope", "/en/champions")]
    [InlineData("/champion/Ahri?lang=english", "/en/champions/Ahri")]
    public async Task InvalidQueryIsIgnoredAsTheOldSiteDid(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));
}
