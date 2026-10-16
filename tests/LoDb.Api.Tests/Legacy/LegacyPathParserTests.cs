using LoDb.Api.Modules.Legacy.Paths;

namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// An old path read before any catalog: one row per form an old route had, then one per
/// form none had. The redirects these forms lead to are tested over HTTP, a row or two per
/// kind.
/// </summary>
public sealed class LegacyPathParserTests
{
    private const string Unknown = "unknown";

    [Theory]
    // Pages: the home page, an editorial page, an account page moved under account/, and the
    // three parameterized ones.
    [InlineData("/home", "page /")]
    [InlineData("/about/data", "page /about/data")]
    [InlineData("/reset-password/check-email", "page /account/forgot-password")]
    [InlineData("/u/some.player_42", "page /u/some.player_42")]
    [InlineData("/reset-password/reset/abcDEF123_-x", "page /account/reset-password/abcDEF123_-x")]
    [InlineData("/builds/42/import", "page /account/builds/42/import")]
    // Catalog: a list without and with its version, written without the leading slash and
    // with a trailing one; a detail without and with its version.
    [InlineData("/objects", "list Items")]
    [InlineData("/7.21.1/runes", "list Runes 7.21.1")]
    [InlineData("champions/", "list Champions")]
    [InlineData("/summoner/SummonerFlash_Jade", "detail Summoners SummonerFlash_Jade")]
    [InlineData("/16.18.1/object/1004", "detail Items 1004 16.18.1")]
    // The routes older still: a detail as the unversioned one, a list with up to two numbers,
    // which are dropped.
    [InlineData("/champion_redirect/Lucian", "detail Champions Lucian")]
    [InlineData("/object_redirect/223085", "detail Items 223085")]
    [InlineData("/rune_redirect/Resolve", "detail Runes Resolve")]
    [InlineData("/summoner_redirect/Summoner_UltBookSmitePlaceholder",
        "detail Summoners Summoner_UltBookSmitePlaceholder")]
    [InlineData("/objects_redirect", "list Items")]
    [InlineData("/champions_redirect/13", "list Champions")]
    [InlineData("/summoners_redirect/1/16/", "list Summoners")]
    [InlineData("/runes_redirect/33/706", "list Runes")]
    public void OldRouteIsReadIntoItsForm(string path, string form) =>
        Assert.Equal(form, Describe(LegacyPathParser.Parse(path)));

    [Theory]
    [InlineData("/")]
    [InlineData("/Home")]
    [InlineData("/about/team")]
    [InlineData("/profile/password")]
    [InlineData("/u/ab")]
    [InlineData("/builds/abc/edit")]
    [InlineData("/champion")]
    [InlineData("/champions/Ahri")]
    [InlineData("/16.19.1")]
    [InlineData("/16.19.1/home")]
    [InlineData("/en/champions")]
    [InlineData("/champion_redirect")]
    [InlineData("/champion_redirect/Ahri/skins")]
    [InlineData("/champions_redirect/Ahri")]
    [InlineData("/objects_redirect/9/16/2")]
    [InlineData("/objects_redirect/-1")]
    [InlineData("/Objects_redirect/9")]
    [InlineData("/object_redirection/3112")]
    [InlineData("/build_redirect/42")]
    [InlineData("/16.19.1/champion_redirect/Ahri")]
    [InlineData("/16.19.1/objects_redirect")]
    public void PathNoOldRouteHadIsUnknown(string path) =>
        Assert.Equal(Unknown, Describe(LegacyPathParser.Parse(path)));

    private static string Describe(LegacyPath? path) => path switch
    {
        null => Unknown,
        { Kind: LegacyPathKind.Page } => $"page /{path.PagePath}",
        { Kind: LegacyPathKind.List } => $"list {path.Resource} {path.Version}".TrimEnd(),
        _ => $"detail {path.Resource} {path.Name} {path.Version}".TrimEnd(),
    };
}
