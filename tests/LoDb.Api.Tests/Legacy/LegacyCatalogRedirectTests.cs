namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// The catalog rows of the 301 table (plan-migration.md): lists and details, with and without
/// <c>?lang=</c> and version. 16.19.1 is the latest recorded version, 16.18.1 the one before.
/// </summary>
[Collection(LegacyApiGroup.Name)]
public sealed class LegacyCatalogRedirectTests(LegacyApiFixture api)
{
    [Theory]
    [InlineData("/champions", "/en/champions")]
    [InlineData("/champions?lang=fr_FR", "/fr/champions")]
    [InlineData("/objects", "/en/items")]
    [InlineData("/objects?lang=ko_KR", "/ko/items")]
    [InlineData("/runes", "/en/runes")]
    [InlineData("/runes?lang=zh_CN", "/zh-hans/runes")]
    [InlineData("/summoners", "/en/summoners")]
    [InlineData("/summoners?lang=ar_AE", "/ar/summoners")]
    [InlineData("/16.18.1/champions", "/en/16.18.1/champions")]
    [InlineData("/16.18.1/objects?lang=fr_FR", "/fr/16.18.1/items")]
    [InlineData("/7.21.1/runes", "/en/7.21.1/runes")]
    [InlineData("/16.18.1/summoners", "/en/16.18.1/summoners")]
    [InlineData("/champions?version=16.18.1", "/en/16.18.1/champions")]
    [InlineData("/objects?version=16.18.1&lang=fr_FR", "/fr/16.18.1/items")]
    [InlineData("/champions/", "/en/champions")]
    public async Task ListLandsOnItsLocaleAndVersion(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    [Theory]
    [InlineData("/champion/Ahri", "/en/champions/Ahri")]
    [InlineData("/champion/MonkeyKing?lang=fr_FR", "/fr/champions/MonkeyKing")]
    [InlineData("/object/1004", "/en/items/1004-faerie-charm")]
    [InlineData("/object/3078?lang=ko_KR", "/ko/items/3078-trinity-force")]
    [InlineData("/rune/Domination", "/en/runes/8100-domination")]
    [InlineData("/rune/Precision?lang=zh_CN", "/zh-hans/runes/8000-precision")]
    [InlineData("/summoner/SummonerFlash", "/en/summoners/SummonerFlash")]
    [InlineData("/summoner/SummonerFlash_Jade?lang=fr_FR", "/fr/summoners/SummonerFlash_Jade")]
    public async Task DetailLandsOnTheCanonicalPath(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    [Theory]
    [InlineData("/16.18.1/champion/Garen", "/en/16.18.1/champions/Garen")]
    [InlineData("/16.18.1/object/1004?lang=fr_FR", "/fr/16.18.1/items/1004-faerie-charm")]
    [InlineData("/16.18.1/rune/Domination?lang=ko_KR", "/ko/16.18.1/runes/8100-domination")]
    [InlineData("/16.18.1/summoner/SummonerHeal", "/en/16.18.1/summoners/SummonerHeal")]
    [InlineData("/champion/Teemo?version=16.18.1", "/en/16.18.1/champions/Teemo")]
    [InlineData("/object/1004?version=16.18.1&lang=zh_CN",
        "/zh-hans/16.18.1/items/1004-faerie-charm")]
    public async Task DetailOfAPastVersionKeepsItsVersion(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    [Theory]
    [InlineData("/16.19.1/champions", "/en/champions")]
    [InlineData("/16.19.1/objects?lang=fr_FR", "/fr/items")]
    [InlineData("/champions?version=16.19.1", "/en/champions")]
    [InlineData("/16.19.1/champion/Ahri", "/en/champions/Ahri")]
    [InlineData("/16.19.1/object/1004?lang=fr_FR", "/fr/items/1004-faerie-charm")]
    [InlineData("/16.19.1/rune/Precision", "/en/runes/8000-precision")]
    [InlineData("/summoner/SummonerHeal?version=16.19.1", "/en/summoners/SummonerHeal")]
    public async Task LatestVersionLandsOnTheShortUrlInOneHop(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    [Theory]
    [InlineData("/champions?version=nope", "/en/champions")]
    [InlineData("/champions?version=9.9.9", "/en/champions")]
    [InlineData("/champion/Ahri?lang=english", "/en/champions/Ahri")]
    [InlineData("/champion/Ahri?lang=en_GB", "/en/champions/Ahri")]
    [InlineData("/champion/Ahri?lang=xx_YY", "/en/champions/Ahri")]
    public async Task InvalidQueryIsIgnoredAsTheOldSiteDid(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));
}
