using System.Globalization;
using System.Net;
using LoDb.Api.Modules.Seo;
using LoDb.Api.Modules.Seo.IndexNow;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// IndexNow: the promotion of a patch submits, in every locale and in requests of 10,000 URLs
/// at most, the short URLs, the pages of changes and the openers of the states it closed, under
/// the key the site publishes at <c>/{key}.txt</c>; without a key, one line tells what would be
/// sent. Any other name, and a key the protocol refuses, go nowhere.
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
        var urls = UrlsOf(submissions);
        Assert.Equal(urls.Count, urls.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal($"{Origin}/ar/", urls[0]);
        Assert.Contains($"{Origin}/fr/items/1004-faerie-charm", urls);
        Assert.Contains($"{Origin}/zh-hant/champions", urls);
        Assert.DoesNotContain($"{Origin}/fr/about", urls);
        Assert.Equal(0, urls.Count % UiLocales.All.Count);
    }

    [Fact]
    public void PromotionSubmitsThePagesOfChangesAndTheClosedOpenersLast()
    {
        var urls = UrlsOf(api.IndexNow.Submissions);
        var trinity = $"{Origin}/fr/{SeoStates.Previous}/items/3078-trinity-force";

        Assert.Contains($"{Origin}/fr/changes", urls);
        Assert.Contains($"{Origin}/fr/{SeoStates.Previous}/changes", urls);
        Assert.Contains(trinity, urls);
        Assert.Equal(
            UiLocales.All.Count,
            urls.Count(static url => url.EndsWith(
                $"/{SeoStates.Previous}/items/3078-trinity-force",
                StringComparison.Ordinal)));
        Assert.DoesNotContain($"{Origin}/fr/{SeoStates.Archived}/items/3371-molten-edge", urls);
        Assert.DoesNotContain($"{Origin}/fr/{SeoStates.Archived}/changes", urls);
        Assert.Equal($"{Origin}/zh-hant/{SeoStates.Previous}/items/3078-trinity-force", urls[^1]);
        Assert.True(urls.IndexOf($"{Origin}/fr/changes") > urls.IndexOf($"{Origin}/zh-hant/"));
    }

    [Fact]
    public async Task WithoutKeyThePromotionLogsWhatItWouldSubmit()
    {
        var promotion = api.IndexNow.Submissions;
        var logger = new FakeLogger<IndexNowSubmitter>();
        var submitter = SubmitterOf(new SeoOptions { CanonicalOrigin = Origin }, logger);

        await submitter.OnPromotedAsync(PatchVersion.Parse(SeoStates.Latest), SeoApiFixture.Token);

        var line = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal((LogLevel.Information, "seo.indexnow.planned"), (line.Level, line.Id.Name));
        Assert.Equal(Text(UrlsOf(promotion).Count), line.GetStructuredStateValue("Urls"));
        Assert.Equal(Text(promotion.Count), line.GetStructuredStateValue("Requests"));
        Assert.Equal(Text(2 * UiLocales.All.Count), line.GetStructuredStateValue("PatchPages"));
        Assert.Equal(Text(UiLocales.All.Count), line.GetStructuredStateValue("Openers"));
        Assert.Equal(promotion.Count, api.IndexNow.Submissions.Count);
    }

    [Fact]
    public async Task VersionTheIndexDoesNotCoverIsNeitherPlannedNorSubmitted()
    {
        var sent = api.IndexNow.Submissions.Count;
        var logger = new FakeLogger<IndexNowSubmitter>();
        var submitter = SubmitterOf(
            new SeoOptions { CanonicalOrigin = Origin, IndexNowKey = Key },
            logger);

        await submitter.OnPromotedAsync(PatchVersion.Parse("99.1.1"), SeoApiFixture.Token);

        var line = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal((LogLevel.Warning, "seo.indexnow.unindexed"), (line.Level, line.Id.Name));
        Assert.Equal(sent, api.IndexNow.Submissions.Count);
    }

    [Fact]
    public async Task PromotionAfterAVersionNeverPromotedAnnouncesWhatChangedSinceThePreviousOne()
    {
        var sent = api.IndexNow.Submissions.Count;
        var submitter = SubmitterOf(
            new SeoOptions { CanonicalOrigin = Origin, IndexNowKey = Key },
            new FakeLogger<IndexNowSubmitter>());
        api.States.Snapshot = SeoStates.SkippedPrevious();
        try
        {
            await submitter.OnPromotedAsync(
                PatchVersion.Parse(SeoStates.Latest),
                SeoApiFixture.Token);
        }
        finally
        {
            api.States.Snapshot = SeoStates.Snapshot();
        }

        var urls = UrlsOf([.. api.IndexNow.Submissions.Skip(sent)]);
        Assert.Contains($"{Origin}/fr/{SeoStates.Previous}/changes", urls);
        Assert.Contains($"{Origin}/fr/{SeoStates.Archived}/changes", urls);
        Assert.Contains($"{Origin}/fr/{SeoStates.Previous}/items/3078-trinity-force", urls);
        Assert.Contains($"{Origin}/fr/{SeoStates.Archived}/items/3371-molten-edge", urls);
        Assert.Contains($"{Origin}/fr/{SeoStates.Start}/items/1001-boots", urls);
        Assert.DoesNotContain($"{Origin}/fr/{SeoStates.Unchanged}/changes", urls);
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

    private static string Text(int count) => count.ToString(CultureInfo.InvariantCulture);

    private static List<string> UrlsOf(IReadOnlyList<FakeIndexNow.Submission> submissions) =>
    [
        .. submissions
            .SelectMany(static sent => sent.Body.GetProperty("urlList").EnumerateArray())
            .Select(static url => url.GetString()!),
    ];

    // A submitter of the API's services but its settings and its log.
    private IndexNowSubmitter SubmitterOf(SeoOptions settings, ILogger<IndexNowSubmitter> logger)
    {
        var services = api.Services;
        return new IndexNowSubmitter(
            services.GetRequiredService<CrawlerCatalog>(),
            services.GetRequiredService<IEntityStateIndex>(),
            services.GetRequiredService<IndexNowClient>(),
            Options.Create(settings),
            logger);
    }
}
