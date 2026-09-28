namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// The page rows of the 301 table: the home page, the editorial and community pages under
/// the locale, the account pages under <c>account/</c>. A version never enters their path.
/// </summary>
[Collection(LegacyApiGroup.Name)]
public sealed class LegacyPageRedirectTests(LegacyApiFixture api)
{
    [Theory]
    [InlineData("/home", "/en/")]
    [InlineData("/home?lang=fr_FR", "/fr/")]
    [InlineData("/home?version=16.18.1", "/en/")]
    [InlineData("/trends", "/en/trends")]
    [InlineData("/trends?lang=ko_KR", "/ko/trends")]
    [InlineData("/about", "/en/about")]
    [InlineData("/about/data?lang=fr_FR", "/fr/about/data")]
    [InlineData("/faq", "/en/faq")]
    [InlineData("/changelog?lang=zh_CN", "/zh-hans/changelog")]
    [InlineData("/developers", "/en/developers")]
    [InlineData("/donate?lang=ar_AE", "/ar/donate")]
    [InlineData("/legal/notice", "/en/legal/notice")]
    [InlineData("/legal/privacy?lang=fr_FR", "/fr/legal/privacy")]
    [InlineData("/legal/terms", "/en/legal/terms")]
    [InlineData("/legal/cookies?version=16.18.1", "/en/legal/cookies")]
    [InlineData("/u/Faker", "/en/u/Faker")]
    [InlineData("/u/some.player_42?lang=fr_FR", "/fr/u/some.player_42")]
    public async Task PublicPageKeepsItsPathUnderTheLocale(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    [Theory]
    [InlineData("/login", "/en/account/login")]
    [InlineData("/login?lang=fr_FR", "/fr/account/login")]
    [InlineData("/register", "/en/account/register")]
    [InlineData("/profile", "/en/account/profile")]
    [InlineData("/profile/preview?lang=ko_KR", "/ko/account/profile/preview")]
    [InlineData("/profile/api", "/en/account/api")]
    [InlineData("/reset-password", "/en/account/forgot-password")]
    [InlineData("/reset-password/check-email", "/en/account/forgot-password")]
    [InlineData("/reset-password/reset/abcDEF123_-x", "/en/account/reset-password/abcDEF123_-x")]
    [InlineData("/builds", "/en/account/builds")]
    [InlineData("/builds/new?lang=fr_FR", "/fr/account/builds/new")]
    [InlineData("/builds/42/edit", "/en/account/builds/42/edit")]
    [InlineData("/builds/42/import?version=16.18.1", "/en/account/builds/42/import")]
    public async Task AccountPageMovesUnderAccount(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));
}
