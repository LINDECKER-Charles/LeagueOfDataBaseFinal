using LoDb.Api.Modules.Analytics.Pages;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;

namespace LoDb.Api.Tests.Analytics;

/// <summary>
/// The grammar of the counted pages, before any catalog: one path per page whatever its
/// trailing slash, the first <c>lang</c> and <c>version</c> of the query, nothing else.
/// </summary>
public sealed class PageAddressParserTests
{
    [Theory]
    [InlineData("/fr", "/fr/")]
    [InlineData("/fr/", "/fr/")]
    [InlineData("/fr/items", "/fr/items")]
    [InlineData("/fr/items/", "/fr/items")]
    [InlineData("/fr/items/1004-faerie-charm/", "/fr/items/1004-faerie-charm")]
    [InlineData("/zh-hans/champions/Ahri#lore", "/zh-hans/champions/Ahri")]
    [InlineData("/en/runes?lang=en_US#x", "/en/runes")]
    public void PathIsOnePerPage(string target, string path) =>
        Assert.Equal(path, PageAddressParser.Parse(target)?.Path);

    [Fact]
    public void DetailIsCutIntoLocaleResourceAndEntry()
    {
        var address = PageAddressParser.Parse("/ko/items/1004-faerie-charm");

        Assert.NotNull(address);
        Assert.Equal(
            (UiLocale.Ko, (ResourceType?)ResourceType.Items, "1004-faerie-charm"),
            (address.Locale, address.Resource, address.Entry));
    }

    [Fact]
    public void HomeHasNoResource()
    {
        var address = PageAddressParser.Parse("/en/");

        Assert.NotNull(address);
        Assert.Equal((UiLocale.En, (ResourceType?)null), (address.Locale, address.Resource));
        Assert.Null(address.Entry);
    }

    [Theory]
    [InlineData("/fr/items?lang=ko_KR&version=16.18.1", "ko_KR", "16.18.1")]
    [InlineData("/fr/items?lang=ko_KR&lang=fr_FR", "ko_KR", null)]
    [InlineData("/fr/items?lang=%20fr_FR%20", "fr_FR", null)]
    [InlineData("/fr/items?lang=&version=%20", null, null)]
    [InlineData("/fr/items?utm_source=x", null, null)]
    public void QueryGivesTheFirstLangAndVersion(string target, string? lang, string? version)
    {
        var address = PageAddressParser.Parse(target);

        Assert.NotNull(address);
        Assert.Equal((lang, version), (address.Lang, address.Version));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("fr/items")]
    [InlineData("/xx")]
    [InlineData("/FR/items")]
    [InlineData("/fr//items")]
    [InlineData("/fr/items//")]
    [InlineData("/fr/builds")]
    [InlineData("/fr/16.19.1/items")]
    [InlineData("/fr/items/1004-faerie-charm/extra")]
    [InlineData("/build/app.js")]
    public void OtherAddressesAreNoCountedPage(string target) =>
        Assert.Null(PageAddressParser.Parse(target));
}
