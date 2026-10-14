using LoDb.Api.Modules.Seo;
using LoDb.Api.Modules.Seo.Sitemaps;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The names below <c>/sitemaps/</c> and what the index and a primary sitemap list, before
/// any catalog is read.
/// </summary>
public sealed class SitemapFileTests
{
    private const string Origin = "https://league-of-data-base.com";

    [Fact]
    public void ReadsTheLatestFile()
    {
        Assert.True(SitemapFile.TryRead("latest.xml", out var file));
        Assert.True(file.IsLatest);
        Assert.Null(file.Version);
    }

    [Fact]
    public void ReadsAVersionFile()
    {
        Assert.True(SitemapFile.TryRead("16.18.1.xml", out var file));
        Assert.Equal(PatchVersion.Parse("16.18.1"), file.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("latest")]
    [InlineData("latest.xml.gz")]
    [InlineData("LATEST.xml")]
    [InlineData("16.xml")]
    [InlineData("16.18.1.2a.xml")]
    [InlineData("lolpatch_7.20.xml")]
    [InlineData("../16.18.1.xml")]
    public void RefusesAnyOtherName(string? name)
    {
        Assert.False(SitemapFile.TryRead(name, out var file));
        Assert.Null(file);
    }

    [Fact]
    public void BuildsLocalePrefixedPaths()
    {
        Assert.Equal("/sitemaps/en/latest.xml", SitemapFile.PathOf(UiLocale.En, null));
        Assert.Equal(
            "/sitemaps/zh-hant/16.18.1.xml",
            SitemapFile.PathOf(UiLocale.ZhHant, PatchVersion.Parse("16.18.1")));
    }

    [Fact]
    public void IndexesEveryLocaleLatestFirstThenEachPastVersion()
    {
        var promoted = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        var versions = new CatalogVersions
        {
            Latest = PatchVersion.Parse("16.19.1"),
            LatestPromotedAt = promoted,
            Listed = [PatchVersion.Parse("16.19.1"), PatchVersion.Parse("16.18.1")],
            Ready = [PatchVersion.Parse("16.19.1")],
        };

        var entries = SitemapLocations.Index(Origin, versions, window: 1).ToList();
        var sitemaps = entries.Select(static entry => entry.Location).ToList();

        Assert.Equal(2 * UiLocales.All.Count, sitemaps.Count);
        Assert.All(entries.Take(UiLocales.All.Count), entry =>
            Assert.Equal(promoted, entry.LastModified));
        Assert.All(entries.Skip(UiLocales.All.Count), static entry =>
            Assert.Null(entry.LastModified));
        Assert.Equal($"{Origin}/sitemaps/ar/latest.xml", sitemaps[0]);
        Assert.Equal($"{Origin}/sitemaps/ar/16.18.1.xml", sitemaps[UiLocales.All.Count]);
        Assert.DoesNotContain(
            sitemaps,
            static url => url.Contains("16.19.1", StringComparison.Ordinal));
    }

    [Fact]
    public void ListsTheStaticPagesBeforeAnyPromotion()
    {
        var prefix = SitePages.Prefix(Origin, UiLocale.Fr, null);
        var entries = SitemapLocations.Latest(prefix, null, null).ToList();
        var pages = entries.Select(static entry => entry.Location).ToList();

        Assert.Equal($"{Origin}/fr/", pages[0]);
        Assert.Contains($"{Origin}/fr/champions", pages);
        Assert.Contains($"{Origin}/fr/legal/cookies", pages);
        Assert.Equal(pages.Count, pages.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(
            pages,
            static url => url.Contains("/account/", StringComparison.Ordinal));
        Assert.All(entries, static entry => Assert.Null(entry.LastModified));
    }

    [Fact]
    public void DatesThePagesOfThePatchByItsPromotionAndNeverTheEditorialOnes()
    {
        var promoted = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        var prefix = SitePages.Prefix(Origin, UiLocale.Fr, null);

        var dates = SitemapLocations.Latest(prefix, null, promoted)
            .ToDictionary(static entry => entry.Location, static entry => entry.LastModified);

        Assert.Equal(promoted, dates[$"{Origin}/fr/"]);
        Assert.Equal(promoted, dates[$"{Origin}/fr/champions"]);
        Assert.Equal(promoted, dates[$"{Origin}/fr/patches"]);
        Assert.Null(dates[$"{Origin}/fr/about"]);
        Assert.Null(dates[$"{Origin}/fr/legal/cookies"]);
    }
}
