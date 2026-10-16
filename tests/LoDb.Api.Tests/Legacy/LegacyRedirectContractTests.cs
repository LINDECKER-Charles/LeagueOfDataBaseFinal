using System.Net;
using System.Text.Json;

namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// What every redirect promises: a short-lived 301, a target that is no old URL (one hop),
/// and a real 404 for whatever the old site did not serve.
/// </summary>
[Collection(LegacyApiGroup.Name)]
public sealed class LegacyRedirectContractTests(LegacyApiFixture api)
{
    private const string ProblemContentType = "application/problem+json";

    // One old URL per kind of target: a page, a list, a detail. Every target starts with its
    // locale (LegacyTargetTests), which no old route had.
    public static TheoryData<string> OldUrls =>
    [
        "/home", "/16.18.1/objects", "/object/1004?lang=fr_FR", "/objects_redirect/9/16",
        "/champion_redirect/Ahri",
    ];

    [Fact]
    public async Task RedirectIsPermanentAndCachedShortly()
    {
        using var response = await api.SendAsync("/champion/Ahri?lang=fr_FR");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/fr/champions/Ahri", response.Headers.Location?.OriginalString);
        Assert.Equal(TimeSpan.FromHours(1), response.Headers.CacheControl?.MaxAge);
        Assert.True(response.Headers.CacheControl?.Public);
    }

    [Theory]
    [InlineData("/champion/ahri", "/en/champions/Ahri")]
    [InlineData("/champion/wukong", "/en/champions/MonkeyKing")]
    public async Task ChampionTypedLooselyMovesToItsPage(string oldUrl, string page) =>
        Assert.Equal(page, await api.LocationOfAsync(oldUrl));

    [Fact]
    public async Task HeadIsAnsweredLikeGet()
    {
        using var response = await api.SendAsync("/objects?lang=fr_FR", HttpMethod.Head);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/fr/items", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [MemberData(nameof(OldUrls))]
    public async Task TargetIsNoOldUrl(string oldUrl)
    {
        var target = await api.LocationOfAsync(oldUrl);

        await AssertProblemAsync(target, "unknown-legacy-url");
    }

    [Theory]
    [InlineData("/champion/NotAChampion", "unknown-entity")]
    [InlineData("/object/999999", "unknown-entity")]
    [InlineData("/object/7050", "unknown-entity")]
    [InlineData("/rune/8100", "unknown-entity")]
    [InlineData("/summoner/Flash", "unknown-entity")]
    [InlineData("/16.19.1/champion/NotAChampion", "unknown-entity")]
    [InlineData("/object_redirect/999999", "unknown-entity")]
    [InlineData("/champion_redirect/NotAChampion", "unknown-entity")]
    [InlineData("/9.9.9/champions", "unknown-version")]
    [InlineData("/9.9.9/champion/Ahri", "unknown-version")]
    public async Task UnknownNameOrVersionIsNotFound(string oldUrl, string code) =>
        await AssertProblemAsync(oldUrl, code);

    // Which paths no old route had is LegacyPathParserTests': these rows prove the answer.
    [Theory]
    [InlineData("/")]
    [InlineData("/16.19.1/items")]
    [InlineData("/objects_redirect/9/16/2")]
    public async Task PathNoOldRouteHadIsNotFound(string oldUrl) =>
        await AssertProblemAsync(oldUrl, "unknown-legacy-url");

    [Fact]
    public async Task OnlyGetAndHeadAreRedirected()
    {
        using var response = await api.SendAsync("/login", HttpMethod.Post);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private async Task AssertProblemAsync(string oldUrl, string code)
    {
        using var response = await api.SendAsync(oldUrl);
        var body = await response.Content.ReadAsStringAsync(LegacyApiFixture.Token);

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"{oldUrl}: {response.StatusCode} {body}");
        Assert.Equal(ProblemContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
    }
}
