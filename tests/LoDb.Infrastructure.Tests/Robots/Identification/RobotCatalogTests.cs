using LoDb.Infrastructure.Robots.Identification;
using LoDb.Infrastructure.Tests.Robots.Support;

namespace LoDb.Infrastructure.Tests.Robots.Identification;

/// <summary>
/// The catalog names a robot by its user agent, the most specific token first, an unknown one
/// after its own token rather than its library's, and names no browser.
/// </summary>
public sealed class RobotCatalogTests
{
    [Theory]
    [InlineData(UserAgents.Googlebot, "googlebot", RobotCategories.Search)]
    [InlineData(UserAgents.GooglebotSmartphone, "googlebot", RobotCategories.Search)]
    [InlineData("Googlebot-Image/1.0", "googlebot-image", RobotCategories.Search)]
    [InlineData(UserAgents.Bingbot, "bingbot", RobotCategories.Search)]
    [InlineData(UserAgents.GptBot, "gptbot", RobotCategories.Ai)]
    [InlineData(UserAgents.ChatGptUser, "chatgpt-user", RobotCategories.Assistant)]
    [InlineData(UserAgents.AhrefsBot, "ahrefsbot", RobotCategories.Seo)]
    [InlineData(
        "facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)",
        "facebookexternalhit",
        RobotCategories.Social)]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/79.0 Safari/537.36 BingPreview/1.0b",
        "bingpreview",
        RobotCategories.Social)]
    [InlineData(
        "Mozilla/5.0+(compatible; UptimeRobot/2.0; http://www.uptimerobot.com/)",
        "uptimerobot",
        RobotCategories.Monitoring)]
    [InlineData("curl/8.5.0", "curl", RobotCategories.Tool)]
    [InlineData("python-requests/2.31.0", "python-requests", RobotCategories.Tool)]
    [InlineData(
        "Mozilla/5.0 (X11; Linux x86_64) HeadlessChrome/120.0.0.0 Safari/537.36",
        "headless-chrome",
        RobotCategories.Tool)]
    public void KnownRobotsAreNamed(string agent, string key, string category)
    {
        var profile = RobotCatalog.Identify(agent);

        Assert.NotNull(profile);
        Assert.Equal((key, category), (profile.Key, profile.Category));
    }

    [Theory]
    [InlineData(UserAgents.Chrome)]
    [InlineData(UserAgents.Firefox)]
    public void BrowsersNameNoRobot(string agent) => Assert.Null(RobotCatalog.Identify(agent));

    [Theory]
    [InlineData("Mozilla/5.0 (compatible; ExampleBot/2.1; +https://example.org)", "examplebot")]
    [InlineData("ExampleCrawler/1.0 python-requests/2.31", "examplecrawler")]
    [InlineData("Mozilla/5.0 (compatible; Zeta/3.0; +https://zeta.example)", "zeta")]
    public void UnknownRobotsAreNamedAfterTheirOwnToken(string agent, string name)
    {
        var profile = RobotCatalog.Identify(agent);

        Assert.NotNull(profile);
        Assert.Equal(RobotCategories.Unidentified, profile.Category);
        Assert.Equal(UnidentifiedRobots.KeyPrefix + name, profile.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void NoUserAgentIsATool(string? agent)
    {
        var profile = RobotCatalog.Identify(agent);

        Assert.Same(RobotCatalog.NoAgent, profile);
        Assert.Equal(RobotCategories.Tool, profile?.Category);
    }

    [Fact]
    public void KeysAreUniqueAndOnlyTheDocumentedOperatorsAreVerifiable()
    {
        var keys = RobotCatalog.Known.Select(static profile => profile.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.True(RobotCatalog.Identify(UserAgents.Googlebot)!.IsVerifiable);
        Assert.False(RobotCatalog.Identify(UserAgents.GptBot)!.IsVerifiable);
    }

    [Fact]
    public void AnUndeclaredRobotIsNamedAfterItsNetwork()
    {
        Assert.Equal("undeclared:example.net", RobotCatalog.Undeclared("example.net").Key);
        Assert.Equal("undeclared:unknown", RobotCatalog.Undeclared(null).Key);
    }
}
