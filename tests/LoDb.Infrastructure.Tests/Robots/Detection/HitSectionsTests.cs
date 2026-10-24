using LoDb.Infrastructure.Robots.Detection;

namespace LoDb.Infrastructure.Tests.Robots.Detection;

/// <summary>A request's section is read from its path alone, by the URL grammar.</summary>
public sealed class HitSectionsTests
{
    [Theory]
    [InlineData("/", HitSections.Home)]
    [InlineData("/fr", HitSections.Home)]
    [InlineData("/zh-hans/?lang=zh_CN", HitSections.Home)]
    [InlineData("/fr/items", HitSections.List)]
    [InlineData("/en/champions/Ahri", HitSections.Detail)]
    [InlineData("/fr/16.1.1/items/1036-long-sword", HitSections.Archive)]
    [InlineData("/fr/patches", HitSections.Page)]
    [InlineData("/sitemap-champions.xml", HitSections.Sitemap)]
    [InlineData("/robots.txt", HitSections.Robots)]
    [InlineData("/api/catalog/16.1.1/en_US", HitSections.Api)]
    [InlineData("/v1/champions", HitSections.Api)]
    [InlineData("/cdn/blobs/0123.webp", HitSections.Asset)]
    [InlineData("/main-ABCD1234.js", HitSections.Asset)]
    [InlineData("/wp-login", HitSections.Other)]
    public void SectionFollowsThePath(string target, string section) =>
        Assert.Equal(section, HitSections.Of(target));

    [Fact]
    public void OnlyPagesCountTowardsAPace()
    {
        Assert.True(HitSections.IsPage(HitSections.Archive));
        Assert.False(HitSections.IsPage(HitSections.Sitemap));
        Assert.False(HitSections.IsPage(HitSections.Asset));
    }
}
