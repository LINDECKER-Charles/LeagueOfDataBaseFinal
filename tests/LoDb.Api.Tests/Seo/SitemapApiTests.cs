using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Catalog;
using LoDb.Domain.Languages;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The sitemaps as a crawler gets them: the index of the latest version and of a window of
/// past ones, the primary sitemap of each locale with the paths the catalog answers, and the
/// sitemap of a past version, pending until its datasets are stored, served immutable once
/// they are, inside the window or not.
/// </summary>
[Collection(SeoApiGroup.Name)]
public sealed class SitemapApiTests(SeoApiFixture api)
{
    internal const string Origin = "http://localhost";
    private const string XmlType = "application/xml";
    private const string Hour = "public, max-age=3600";

    private static readonly string[] Lists = ["champions", "items", "runes", "summoners"];

    [Fact]
    public async Task IndexListsEveryLocaleOfTheLatestAndTheWindowOfPastVersions()
    {
        using var response = await api.GetAsync("/sitemap.xml");
        var index = SitemapXmlTests.Parse(await BodyAsync(response));
        var locations = SitemapXmlTests.Locations(index);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XmlType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Hour, CacheHeaderTests.CacheControlOf(response));
        Assert.Equal(SitemapXmlTests.Sitemaps + "sitemapindex", index.Root!.Name);
        Assert.Equal(
            (1 + SeoApiFixture.SitemapVersionWindow) * UiLocales.All.Count,
            locations.Count);
        Assert.Equal($"{Origin}/sitemaps/ar/latest.xml", locations[0]);
        Assert.Contains($"{Origin}/sitemaps/zh-hant/16.18.1.xml", locations);
        Assert.DoesNotContain($"{Origin}/sitemaps/en/16.19.1.xml", locations);
        Assert.DoesNotContain(
            $"{Origin}/sitemaps/en/{SeoApiFixture.Archived.Value}.xml",
            locations);
    }

    [Fact]
    public async Task PastVersionOutsideTheWindowKeepsItsImmutableSitemap()
    {
        var archived = SeoApiFixture.Archived.Value;

        using var response = await api.GetAsync($"/sitemaps/fr/{archived}.xml");
        var pages = SitemapXmlTests.Locations(SitemapXmlTests.Parse(await BodyAsync(response)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "public, max-age=31536000, immutable",
            CacheHeaderTests.CacheControlOf(response));
        Assert.Equal(
            Lists.Select(list => $"{Origin}/fr/{archived}/{list}"),
            pages.Take(Lists.Length));
        Assert.Contains($"{Origin}/fr/{archived}/champions/Garen", pages);
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
        var document = SitemapXmlTests.Parse(await BodyAsync(response));

        var dates = document.Root!.Elements().ToDictionary(
            static url => url.Element(SitemapXmlTests.Sitemaps + "loc")!.Value,
            static url => url.Element(SitemapXmlTests.Sitemaps + "lastmod")?.Value);
        var promoted = dates[$"{Origin}/fr/items/1004-faerie-charm"];
        Assert.NotNull(promoted);
        Assert.True(DateTimeOffset.TryParse(promoted, CultureInfo.InvariantCulture, out _));
        Assert.Equal(promoted, dates[$"{Origin}/fr/"]);
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

    [Fact]
    public async Task ColdVersionIsQueuedAndAnswersLater()
    {
        using var response = await api.GetAsync("/sitemaps/de/16.18.1.xml");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.RetryAfter?.Delta);
        Assert.Equal("no-store", CacheHeaderTests.CacheControlOf(response));
    }

    [Theory]
    [InlineData("/sitemaps/en/99.1.1.xml")]
    [InlineData("/sitemaps/xx/latest.xml")]
    [InlineData("/sitemaps/sitemap.xml")]
    public async Task AnythingElseIsMissing(string path)
    {
        using var response = await api.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task<byte[]> BodyAsync(HttpResponseMessage response) =>
        response.Content.ReadAsByteArrayAsync(SeoApiFixture.Token);

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
