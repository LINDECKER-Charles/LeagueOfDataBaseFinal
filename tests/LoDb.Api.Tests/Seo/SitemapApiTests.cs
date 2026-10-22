using System.Globalization;
using System.Net;
using System.Xml.Linq;
using LoDb.Api.Tests.Catalog;
using LoDb.Domain.Languages;
using LoDb.Testing.States;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The sitemaps as a crawler gets them: the index of the latest version and of every covered
/// past version with something to index, the primary sitemap of each locale with the paths the
/// catalog answers, and the sitemap of a past version read in the index of states alone
/// (<see cref="SeoStates"/>).
/// </summary>
[Collection(SeoApiGroup.Name)]
public sealed class SitemapApiTests(SeoApiFixture api)
{
    internal const string Origin = "http://localhost";
    private const string XmlType = "application/xml";
    private const string Hour = "public, max-age=3600";

    private static readonly string[] Lists = ["champions", "items", "runes", "summoners"];

    [Fact]
    public async Task IndexListsTheLatestThenEveryPastFileThatListsSomething()
    {
        using var response = await api.GetAsync("/sitemap.xml");
        var index = SitemapXmlTests.Parse(await BodyAsync(response));
        var dates = DatesOf(index);
        var locations = SitemapXmlTests.Locations(index);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XmlType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Hour, CacheHeaderTests.CacheControlOf(response));
        Assert.Equal(SitemapXmlTests.Sitemaps + "sitemapindex", index.Root!.Name);
        Assert.Equal(4 * UiLocales.All.Count, locations.Count);
        Assert.Equal($"{Origin}/sitemaps/ar/latest.xml", locations[0]);
        Assert.Equal($"{Origin}/sitemaps/ar/{SeoStates.Previous}.xml", locations[21]);
        Assert.Equal($"{Origin}/sitemaps/zh-hant/{SeoStates.Start}.xml", locations[^1]);
        Assert.Contains($"{Origin}/sitemaps/de/{SeoStates.Archived}.xml", locations);
        Assert.DoesNotContain($"{Origin}/sitemaps/en/{SeoStates.Unchanged}.xml", locations);
        Assert.DoesNotContain($"{Origin}/sitemaps/en/{SeoStates.Latest}.xml", locations);
        Assert.Equal(
            Lastmod(SeoStates.TrinityClosedAt),
            dates[$"{Origin}/sitemaps/fr/{SeoStates.Previous}.xml"]);
    }

    [Fact]
    public async Task PastVersionListsItsPatchPageThenTheOpenersOfItsClosedStates()
    {
        var previous = SeoStates.Previous;

        using var response = await api.GetAsync($"/sitemaps/fr/{previous}.xml");
        var document = SitemapXmlTests.Parse(await BodyAsync(response));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Hour, CacheHeaderTests.CacheControlOf(response));
        Assert.Equal(
            [
                $"{Origin}/fr/{previous}/changes",
                $"{Origin}/fr/{previous}/items/3078-trinity-force",
            ],
            SitemapXmlTests.Locations(document));
        Assert.Equal(
            [Lastmod(SeoStates.LatestPromotedAt), Lastmod(SeoStates.TrinityClosedAt)],
            DatesOf(document).Values);
    }

    [Fact]
    public async Task CoverageStartHasNoPatchPageButItsClosedOpeners()
    {
        using var response = await api.GetAsync($"/sitemaps/en/{SeoStates.Start}.xml");
        var document = SitemapXmlTests.Parse(await BodyAsync(response));

        Assert.Equal(
            [$"{Origin}/en/{SeoStates.Start}/items/1001-boots"],
            SitemapXmlTests.Locations(document));
        Assert.Equal([Lastmod(TestStates.BuiltAt)], DatesOf(document).Values);
    }

    [Fact]
    public async Task CoveredVersionWithNothingToIndexIsServedEmpty()
    {
        using var response = await api.GetAsync($"/sitemaps/de/{SeoStates.Unchanged}.xml");
        var pages = SitemapXmlTests.Locations(SitemapXmlTests.Parse(await BodyAsync(response)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(pages);
    }

    [Fact]
    public async Task LatestListsTheStaticPagesAndTheCatalogCanonicalPaths()
    {
        using var response = await api.GetAsync("/sitemaps/fr/latest.xml");
        var pages = SitemapXmlTests.Locations(SitemapXmlTests.Parse(await BodyAsync(response)));
        var expected = await CanonicalPathsAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Hour, CacheHeaderTests.CacheControlOf(response));
        Assert.Equal($"{Origin}/fr/", pages[0]);
        Assert.Contains($"{Origin}/fr/about/data", pages);
        Assert.Equal($"{Origin}/fr/patches", pages[5]);
        Assert.Equal($"{Origin}/fr/changes", pages[6]);
        Assert.Contains($"{Origin}/fr/items/1004-faerie-charm", pages);
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            pages.Where(IsEntity).Select(static url => url[$"{Origin}/fr/".Length..])
                .Order(StringComparer.Ordinal));
        Assert.Equal(pages.Count, pages.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task LatestDatesThePagesOfThePatchByItsPromotion()
    {
        using var response = await api.GetAsync("/sitemaps/fr/latest.xml");
        var dates = DatesOf(SitemapXmlTests.Parse(await BodyAsync(response)));

        var promoted = dates[$"{Origin}/fr/items/1004-faerie-charm"];
        Assert.NotNull(promoted);
        Assert.True(DateTimeOffset.TryParse(promoted, CultureInfo.InvariantCulture, out _));
        Assert.Equal(promoted, dates[$"{Origin}/fr/"]);
        Assert.Equal(promoted, dates[$"{Origin}/fr/changes"]);
        Assert.Null(dates[$"{Origin}/fr/about"]);
    }

    [Theory]
    [InlineData("/sitemaps/en/16.19.1.xml", "/sitemaps/en/latest.xml")]
    [InlineData("/sitemaps/latest.xml", "/sitemap.xml")]
    [InlineData("/sitemaps/16.18.1.xml", "/sitemaps/en/16.18.1.xml")]
    [InlineData("/sitemaps/16.19.1.xml", "/sitemaps/en/latest.xml")]
    public async Task MovesLatestAndPreviousFormsInOneShortLivedHop(string path, string target)
    {
        using var response = await api.GetAsync(path);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(target, response.Headers.Location?.OriginalString);
        Assert.Equal(
            "public, max-age=0, s-maxage=60",
            CacheHeaderTests.CacheControlOf(response));
    }

    [Theory]
    [InlineData("/sitemaps/en/99.1.1.xml")]
    [InlineData("/sitemaps/de/3.13.24.xml")]
    [InlineData("/sitemaps/xx/latest.xml")]
    [InlineData("/sitemaps/sitemap.xml")]
    public async Task NewerUncoveredOrUnknownFilesAreMissing(string path)
    {
        using var response = await api.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/sitemap.xml")]
    [InlineData("/sitemaps/fr/latest.xml")]
    [InlineData("/sitemaps/fr/8.7.1.xml")]
    public async Task UnavailableIndexAnswersLater(string path)
    {
        api.States.IsUnavailable = true;
        try
        {
            using var response = await api.GetAsync(path);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.RetryAfter?.Delta);
            Assert.Equal("no-store", CacheHeaderTests.CacheControlOf(response));
        }
        finally
        {
            api.States.IsUnavailable = false;
        }
    }

    [Fact]
    public async Task LaggingIndexAnnouncesItsEndAndNoPageOfChangesForTheLatest()
    {
        api.States.Snapshot = SeoStates.Lagging();
        try
        {
            using var index = await api.GetAsync("/sitemap.xml");
            using var latest = await api.GetAsync("/sitemaps/fr/latest.xml");
            var dates = DatesOf(SitemapXmlTests.Parse(await BodyAsync(index)));
            var pages = SitemapXmlTests.Locations(SitemapXmlTests.Parse(await BodyAsync(latest)));

            Assert.Equal(
                Lastmod(SeoStates.LatestPromotedAt),
                dates[$"{Origin}/sitemaps/fr/{SeoStates.Previous}.xml"]);
            Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
            Assert.Contains($"{Origin}/fr/patches", pages);
            Assert.DoesNotContain($"{Origin}/fr/changes", pages);
        }
        finally
        {
            api.States.Snapshot = SeoStates.Snapshot();
        }
    }

    [Fact]
    public async Task VersionBetweenTheIndexEndAndTheLatestIsMissing()
    {
        api.States.Snapshot = TestStates.Snapshot(
            [SeoStates.Start, SeoStates.Unchanged, SeoStates.Archived],
            []);
        try
        {
            using var response = await api.GetAsync($"/sitemaps/en/{SeoStates.Previous}.xml");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            api.States.Snapshot = SeoStates.Snapshot();
        }
    }

    [Fact]
    public async Task UnavailableIndexStillRedirectsAndRefusesWhatItNeedsNot()
    {
        api.States.IsUnavailable = true;
        try
        {
            using var latest = await api.GetAsync("/sitemaps/en/16.19.1.xml");
            using var newer = await api.GetAsync("/sitemaps/en/99.1.1.xml");

            Assert.Equal(HttpStatusCode.MovedPermanently, latest.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, newer.StatusCode);
        }
        finally
        {
            api.States.IsUnavailable = false;
        }
    }

    private static Task<byte[]> BodyAsync(HttpResponseMessage response) =>
        response.Content.ReadAsByteArrayAsync(SeoApiFixture.Token);

    private static Dictionary<string, string?> DatesOf(XDocument document) =>
        document.Root!.Elements().ToDictionary(
            static entry => entry.Element(SitemapXmlTests.Sitemaps + "loc")!.Value,
            static entry => entry.Element(SitemapXmlTests.Sitemaps + "lastmod")?.Value);

    private static string Lastmod(DateTimeOffset date) =>
        date.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static bool IsEntity(string url) =>
        Lists.Any(list => url.StartsWith($"{Origin}/fr/{list}/", StringComparison.Ordinal));

    private async Task<IReadOnlyList<string>> CanonicalPathsAsync()
    {
        var paths = new List<string>();
        foreach (var list in Lists)
        {
            var answer = await api.GetJsonAsync($"/api/catalog/16.19.1/en_US/{list}");
            paths.AddRange(answer.Pluck("entries", "canonicalPath"));
        }

        return [.. paths.Distinct(StringComparer.Ordinal)];
    }
}
