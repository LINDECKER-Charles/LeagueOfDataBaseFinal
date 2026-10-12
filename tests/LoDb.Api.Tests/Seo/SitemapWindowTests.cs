using LoDb.Api.Modules.Seo;
using LoDb.Api.Modules.Seo.Sitemaps;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The window of the sitemap index: the latest version's sitemaps, then those of the versions
/// listed next, as many as configured (12 by default), never a negative count.
/// </summary>
public sealed class SitemapWindowTests
{
    private const string Origin = "https://league-of-data-base.com";
    private const int ListedCount = 20;

    // 16.20.1 down to 16.1.1, newest first, as Data Dragon lists them.
    private static readonly PatchVersion[] Listed =
    [
        .. Enumerable.Range(0, ListedCount)
            .Select(static step => PatchVersion.Parse($"16.{ListedCount - step}.1")),
    ];

    private static readonly SeoOptionsValidator Validator = new();

    [Fact]
    public void DefaultWindowAnnouncesTheLatestAndTheTwelvePatchesBefore()
    {
        const int Window = SeoOptions.DefaultSitemapVersionWindow;

        var index = IndexOf(Listed[0], new SeoOptions().SitemapVersionWindow);

        Assert.Equal(12, Window);
        Assert.Equal((1 + Window) * UiLocales.All.Count, index.Count);
        Assert.Equal($"{Origin}/sitemaps/ar/latest.xml", index[0]);
        Assert.Contains(SitemapOf(Listed[Window]), index);
        Assert.DoesNotContain(SitemapOf(Listed[Window + 1]), index);
        Assert.DoesNotContain(SitemapOf(Listed[0]), index);
    }

    [Fact]
    public void NoWindowLeavesTheLatestSitemapsAlone()
    {
        var index = IndexOf(Listed[0], window: 0);

        Assert.Equal(UiLocales.All.Count, index.Count);
        Assert.All(
            index,
            static url => Assert.EndsWith("/latest.xml", url, StringComparison.Ordinal));
    }

    [Fact]
    public void VersionNotPromotedYetStaysOutOfTheWindow()
    {
        var index = IndexOf(Listed[1], window: 2);

        Assert.Equal(3 * UiLocales.All.Count, index.Count);
        Assert.DoesNotContain(SitemapOf(Listed[0]), index);
        Assert.Contains(SitemapOf(Listed[2]), index);
        Assert.Contains(SitemapOf(Listed[3]), index);
        Assert.DoesNotContain(SitemapOf(Listed[4]), index);
    }

    [Fact]
    public void BeforeAnyPromotionTheFirstListedVersionsFillTheWindow()
    {
        var index = IndexOf(latest: null, window: 2);

        Assert.Equal(3 * UiLocales.All.Count, index.Count);
        Assert.Contains(SitemapOf(Listed[1]), index);
        Assert.DoesNotContain(SitemapOf(Listed[2]), index);
    }

    [Fact]
    public void AcceptsAWindowOfZero()
    {
        var result = Validator.Validate(null, new SeoOptions { SitemapVersionWindow = 0 });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void RefusesANegativeWindow()
    {
        var result = Validator.Validate(null, new SeoOptions { SitemapVersionWindow = -1 });

        Assert.True(result.Failed);
        Assert.Contains("SitemapVersionWindow", result.FailureMessage, StringComparison.Ordinal);
    }

    private static List<string> IndexOf(PatchVersion? latest, int window)
    {
        var versions = new CatalogVersions { Latest = latest, Listed = Listed, Ready = [] };
        return [.. SitemapLocations.Index(Origin, versions, window)
            .Select(static entry => entry.Location)];
    }

    private static string SitemapOf(PatchVersion version) =>
        $"{Origin}/sitemaps/fr/{version.Value}.xml";
}
