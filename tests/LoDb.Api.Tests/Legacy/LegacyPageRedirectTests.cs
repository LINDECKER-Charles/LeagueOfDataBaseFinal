namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// The page rows of the 301 table: the home page, the editorial and community pages under
/// the locale, the account pages under <c>account/</c>. A version never enters their path.
/// </summary>
/// <remarks>
/// Every old page and its new path is a row of LegacyPathParserTests: these rows prove the
/// endpoint wires the parser, the language and the version that pages leave out.
/// </remarks>
[Collection(LegacyApiGroup.Name)]
public sealed class LegacyPageRedirectTests(LegacyApiFixture api)
{
    [Theory]
    [InlineData("/about/data?lang=fr_FR", "/fr/about/data")]
    [InlineData("/home?version=16.18.1", "/en/")]
    public async Task PublicPageKeepsItsPathUnderTheLocale(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));

    [Theory]
    [InlineData("/login?lang=fr_FR", "/fr/account/login")]
    [InlineData("/builds/42/import?version=16.18.1", "/en/account/builds/42/import")]
    public async Task AccountPageMovesUnderAccount(string oldUrl, string newUrl) =>
        Assert.Equal(newUrl, await api.LocationOfAsync(oldUrl));
}
