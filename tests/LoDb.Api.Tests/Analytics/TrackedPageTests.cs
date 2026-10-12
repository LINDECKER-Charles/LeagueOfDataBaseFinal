using LoDb.Api.Tests.Analytics.Support;
using LoDb.Infrastructure.Analytics.Capture;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Analytics;

/// <summary>
/// The page an address shows, under the legacy stack's names: entity keys per type, the
/// pages the front redirects left out, the status and language the page is served with.
/// 16.19.1 is the latest recorded version, 16.18.1 the one before.
/// </summary>
[Collection(AnalyticsApiGroup.Name)]
public sealed class TrackedPageTests(AnalyticsApiFixture api)
{
    // One row per resource, under the names the legacy aggregates hold. The champion is
    // counted by its key, MonkeyKing, never by its name, Wukong.
    [Theory]
    [InlineData("/fr/champions/MonkeyKing", "app_champion", "champion", "MonkeyKing")]
    [InlineData("/fr/items/1004-faerie-charm", "app_item", "item", "1004")]
    [InlineData("/fr/runes/8100-domination", "app_rune", "runesReforged", "Domination")]
    [InlineData("/en/summoners/SummonerFlash", "app_summoner", "summoner", "SummonerFlash")]
    public async Task DetailIsCountedUnderTheLegacyKey(
        string target,
        string route,
        string type,
        string entity)
    {
        var page = await ResolveAsync(target);

        Assert.NotNull(page);
        Assert.Equal(
            (route, type, "detail", entity, (short)200),
            (page.Route, page.Type, page.Kind, page.Entity, page.Status));
        Assert.Equal(target, page.Path);
    }

    // One row per page, under the names the legacy aggregates hold. The one path a page is
    // counted under, whatever its slash, query or fragment, is PageAddressParserTests'.
    [Theory]
    [InlineData("/fr/", "app_home", "home", "home")]
    [InlineData("/fr/champions", "app_champions", "champion", "list")]
    [InlineData("/ko/items", "app_items", "item", "list")]
    [InlineData("/en/runes", "app_runes", "runesReforged", "list")]
    [InlineData("/ar/summoners", "app_summoners", "summoner", "list")]
    public async Task ListsAndHomeHaveNoEntity(
        string target,
        string route,
        string type,
        string kind)
    {
        var page = await ResolveAsync(target);

        Assert.NotNull(page);
        Assert.Equal(
            (route, type, kind, target, (short)200),
            (page.Route, page.Type, page.Kind, page.Path, page.Status));
        Assert.Null(page.Entity);
    }

    [Theory]
    [InlineData("/fr/items/1004-x")]
    [InlineData("/fr/items?version=16.18.1")]
    [InlineData("/fr/champions/Ahri/skins")]
    public async Task RedirectedOrForeignAddressIsNotCounted(string target) =>
        Assert.Null(await ResolveAsync(target));

    [Theory]
    [InlineData("/fr/champions/Nobody", "Nobody")]
    [InlineData("/fr/items/9999-nothing", "9999")]
    public async Task MissingEntityIsANotFound(string target, string entity)
    {
        var page = await ResolveAsync(target);

        Assert.NotNull(page);
        Assert.Equal(((short)404, entity), (page.Status, page.Entity));
    }

    // Without a language, then with one of another locale (an unknown one goes the same way):
    // the page shows its locale's.
    [Theory]
    [InlineData("/fr/items", "fr_FR")]
    [InlineData("/fr/items?lang=ko_KR", "fr_FR")]
    public async Task LanguageIsTheOneThePageShows(string target, string lang)
    {
        var page = await ResolveAsync(target);

        Assert.NotNull(page);
        Assert.Equal(lang, page.Lang);
    }

    [Fact]
    public async Task HomeKeepsTheVersionItReadsInPlace()
    {
        var pinned = await ResolveAsync("/en/?version=16.18.1");
        var unknown = await ResolveAsync("/en/?version=1.0.0");

        Assert.Equal(("16.18.1", "en"), (pinned?.Version, pinned?.Locale));
        Assert.NotNull(unknown);
        Assert.Null(unknown.Version);
    }

    private Task<TrackedPage?> ResolveAsync(string target) =>
        api.Services.GetRequiredService<ITrackedPageResolver>()
            .ResolveAsync(target, AnalyticsApiFixture.Token);
}
