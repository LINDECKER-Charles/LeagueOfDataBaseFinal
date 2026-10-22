using LoDb.Api.Modules.Seo;
using LoDb.Api.Modules.Seo.Sitemaps;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The names below <c>/sitemaps/</c> and what a primary sitemap lists, before any catalog is
/// read.
/// </summary>
public sealed class SitemapFileTests
{
    private const string Origin = "https://league-of-data-base.com";

    private static readonly LatestPatch NoPromotion = new(null, null, HasChanges: false);

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
    public void ListsTheStaticPagesBeforeAnyPromotion()
    {
        var prefix = SitePages.Prefix(Origin, UiLocale.Fr, null);
        var entries = SitemapLocations.Latest(prefix, null, NoPromotion).ToList();
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

        var latest = new LatestPatch(PatchVersion.Parse("16.19.1"), promoted, HasChanges: true);

        var dates = SitemapLocations.Latest(prefix, null, latest)
            .ToDictionary(static entry => entry.Location, static entry => entry.LastModified);

        Assert.Equal(promoted, dates[$"{Origin}/fr/"]);
        Assert.Equal(promoted, dates[$"{Origin}/fr/champions"]);
        Assert.Equal(promoted, dates[$"{Origin}/fr/patches"]);
        Assert.Equal(promoted, dates[$"{Origin}/fr/changes"]);
        Assert.Null(dates[$"{Origin}/fr/about"]);
        Assert.Null(dates[$"{Origin}/fr/legal/cookies"]);
    }

    [Fact]
    public void LeavesOutThePageOfChangesOfALatestPatchThatChangedNothing()
    {
        var prefix = SitePages.Prefix(Origin, UiLocale.Fr, null);
        var latest = new LatestPatch(PatchVersion.Parse("16.19.1"), null, HasChanges: false);

        var pages = SitemapLocations.Latest(prefix, null, latest)
            .Select(static entry => entry.Location);

        Assert.DoesNotContain($"{Origin}/fr/changes", pages);
    }
}
