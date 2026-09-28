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
        var versions = new CatalogVersions
        {
            Latest = PatchVersion.Parse("16.19.1"),
            Listed = [PatchVersion.Parse("16.19.1"), PatchVersion.Parse("16.18.1")],
            Ready = [PatchVersion.Parse("16.19.1")],
        };

        var sitemaps = SitemapLocations.Index(Origin, versions).ToList();

        Assert.Equal(2 * UiLocales.All.Count, sitemaps.Count);
        Assert.Equal($"{Origin}/sitemaps/ar/latest.xml", sitemaps[0]);
        Assert.Equal($"{Origin}/sitemaps/ar/16.18.1.xml", sitemaps[UiLocales.All.Count]);
        Assert.DoesNotContain(
            sitemaps,
            static url => url.Contains("16.19.1", StringComparison.Ordinal));
    }

    [Fact]
    public void ListsTheStaticPagesBeforeAnyPromotion()
    {
        var pages = SitemapLocations.Latest(Origin, UiLocale.Fr, null).ToList();

        Assert.Equal($"{Origin}/fr/", pages[0]);
        Assert.Contains($"{Origin}/fr/champions", pages);
        Assert.Contains($"{Origin}/fr/legal/cookies", pages);
        Assert.Equal(pages.Count, pages.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(
            pages,
            static url => url.Contains("/account/", StringComparison.Ordinal));
    }
}
