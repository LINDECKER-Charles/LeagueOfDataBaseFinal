using System.Net;
using LoDb.Api.Tests.Catalog;

namespace LoDb.Api.Tests.Seo;

/// <summary><c>/robots.txt</c> and <c>/llms.txt</c> as served, with the request's origin.</summary>
[Collection(SeoApiGroup.Name)]
public sealed class SeoFileApiTests(SeoApiFixture api)
{
    [Fact]
    public async Task RobotsPointsToTheIndexOfThisOrigin()
    {
        using var response = await api.GetAsync("/robots.txt");
        var robots = await response.Content.ReadAsStringAsync(SeoApiFixture.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal("public, max-age=3600", CacheHeaderTests.CacheControlOf(response));
        Assert.StartsWith("User-agent: *\n", robots, StringComparison.Ordinal);
        Assert.EndsWith(
            $"Sitemap: {SitemapApiTests.Origin}/sitemap.xml\n",
            robots,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LlmsCountsWhatTheLatestVersionPublishes()
    {
        using var response = await api.GetAsync("/llms.txt");
        var llms = await response.Content.ReadAsStringAsync(SeoApiFixture.Token);
        var champions = await api.GetJsonAsync("/api/catalog/16.19.1/en_US/champions");
        var items = await api.GetJsonAsync("/api/catalog/16.19.1/en_US/items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/markdown", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Current patch: 16.19.1. It publishes ", llms, StringComparison.Ordinal);
        Assert.Contains(
            $" {champions.Items("entries").Count} champions, {items.Items("entries").Count} items,",
            llms,
            StringComparison.Ordinal);
        Assert.Contains($"({SitemapApiTests.Origin}/en/faq)", llms, StringComparison.Ordinal);
    }
}
