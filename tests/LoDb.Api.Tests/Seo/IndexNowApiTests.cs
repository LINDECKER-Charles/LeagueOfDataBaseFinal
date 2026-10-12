using System.Net;
using LoDb.Api.Modules.Seo;
using LoDb.Domain.Languages;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// IndexNow: the promotion of a patch submits the pages it changed in every locale, in
/// requests of 10,000 URLs at most, under the key the site publishes at <c>/{key}.txt</c>;
/// any other name, and a key the protocol refuses, go nowhere.
/// </summary>
[Collection(SeoApiGroup.Name)]
public sealed class IndexNowApiTests(SeoApiFixture api)
{
    private const string Origin = SeoApiFixture.Origin;
    private const string Key = SeoApiFixture.IndexNowKey;
    private const int MaxUrls = 10_000;

    private static readonly SeoOptionsValidator Validator = new();

    [Fact]
    public void PromotionSubmitsThePagesOfThePatchInEveryLocale()
    {
        var submissions = api.IndexNow.Submissions;

        Assert.NotEmpty(submissions);
        Assert.All(submissions, static submission =>
        {
            Assert.Equal(HttpMethod.Post, submission.Method);
            Assert.Equal(new Uri("https://api.indexnow.org/indexnow"), submission.Url);
            Assert.Equal("application/json", submission.MediaType);
            Assert.Equal("localhost", submission.Body.GetProperty("host").GetString());
            Assert.Equal(Key, submission.Body.GetProperty("key").GetString());
            Assert.Equal(
                $"{Origin}/{Key}.txt",
                submission.Body.GetProperty("keyLocation").GetString());
            Assert.InRange(submission.Body.GetProperty("urlList").GetArrayLength(), 1, MaxUrls);
        });
        var urls = submissions
            .SelectMany(static sent => sent.Body.GetProperty("urlList").EnumerateArray())
            .Select(static url => url.GetString())
            .ToList();
        Assert.Equal(urls.Count, urls.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains($"{Origin}/fr/items/1004-faerie-charm", urls);
        Assert.Contains($"{Origin}/zh-hant/champions", urls);
        Assert.Contains($"{Origin}/ar/", urls);
        Assert.DoesNotContain($"{Origin}/fr/about", urls);
        Assert.Equal(0, urls.Count % UiLocales.All.Count);
    }

    [Fact]
    public async Task KeyFileAnswersTheKeyAndNothingElse()
    {
        using var keyFile = await api.GetAsync($"/{Key}.txt");
        using var otherName = await api.GetAsync("/0123456789abcdef.txt");

        Assert.Equal(HttpStatusCode.OK, keyFile.StatusCode);
        Assert.Equal("text/plain", keyFile.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Key, await keyFile.Content.ReadAsStringAsync(SeoApiFixture.Token));
        Assert.Equal(HttpStatusCode.NotFound, otherName.StatusCode);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("has a space in it")]
    [InlineData("accentué-0123456789")]
    public void RefusesAKeyOutsideTheProtocol(string key)
    {
        var result = Validator.Validate(null, new SeoOptions { IndexNowKey = key });

        Assert.True(result.Failed);
        Assert.Contains("IndexNowKey", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsAKeyOfTheProtocolOrNone()
    {
        Assert.True(Validator.Validate(null, new SeoOptions { IndexNowKey = Key }).Succeeded);
        Assert.True(Validator.Validate(null, new SeoOptions()).Succeeded);
    }
}
