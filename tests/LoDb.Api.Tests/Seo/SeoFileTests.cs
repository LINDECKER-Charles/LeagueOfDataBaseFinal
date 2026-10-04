using LoDb.Api.Modules.Seo.Files;
using LoDb.Domain.Versions;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The content of <c>/robots.txt</c> and <c>/llms.txt</c>, written with the origin they are
/// given.
/// </summary>
public sealed class SeoFileTests
{
    private const string Origin = "https://league-of-data-base.com";

    [Fact]
    public void RobotsIsOneGroupPointingToTheIndex()
    {
        const string Expected = """
            User-agent: *
            Disallow: /admin
            Disallow: /api/
            Disallow: /v1/
            Disallow: /webhooks/
            Disallow: /*/account/
            Disallow: /*/donate/checkout
            Disallow: /*/donate/success
            Disallow: /*/donate/cancel
            Allow: /

            Sitemap: https://league-of-data-base.com/sitemap.xml

            """;

        Assert.Equal(Expected.ReplaceLineEndings("\n"), RobotsTxt.Build(Origin));
    }

    [Fact]
    public void RobotsLetsCrawlersReadTheSharedBuildsNoindex()
    {
        var robots = RobotsTxt.Build(Origin);

        Assert.DoesNotContain("/b/", robots, StringComparison.Ordinal);
        Assert.DoesNotContain("Crawl-delay", robots, StringComparison.Ordinal);
    }

    [Fact]
    public void LlmsLinksTheEnglishPagesAndStatesThePatch()
    {
        var llms = LlmsTxt.Build(Origin, new SiteInventory(PatchVersion.Parse("16.19.1"), null));

        Assert.StartsWith(
            "# League Of Data Base\n\n> Free League of Legends",
            llms,
            StringComparison.Ordinal);
        Assert.Contains(
            "- [https://league-of-data-base.com/en/champions]"
                + "(https://league-of-data-base.com/en/champions): Every champion",
            llms,
            StringComparison.Ordinal);
        Assert.Contains(
            "(https://league-of-data-base.com/en/about/data)",
            llms,
            StringComparison.Ordinal);
        Assert.Contains(
            "permanent URLs of the form https://league-of-data-base.com/en/{patch}/champions/{id}.",
            llms,
            StringComparison.Ordinal);
        Assert.Contains("Current patch: 16.19.1. Every previously", llms, StringComparison.Ordinal);
        Assert.EndsWith("funded by voluntary donations.\n", llms, StringComparison.Ordinal);
    }

    [Fact]
    public void LlmsDropsTheCountsItCouldNotRead()
    {
        var llms = LlmsTxt.Build(Origin, SiteInventory.Empty);

        Assert.Contains("Current patch: unavailable.", llms, StringComparison.Ordinal);
        Assert.DoesNotContain("It publishes", llms, StringComparison.Ordinal);
    }

    [Fact]
    public void LlmsIsMarkdownWithFourSections()
    {
        var llms = LlmsTxt.Build(Origin, SiteInventory.Empty);
        var headings = llms.Split('\n')
            .Where(static line => line.StartsWith("## ", StringComparison.Ordinal));

        Assert.Equal(
            ["## Game data", "## About this project", "## What you can do here", "## Notes"],
            headings);
    }
}
