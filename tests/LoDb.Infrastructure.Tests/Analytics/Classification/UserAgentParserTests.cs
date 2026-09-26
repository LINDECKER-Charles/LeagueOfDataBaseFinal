using LoDb.Infrastructure.Analytics.Classification;
using LoDb.Infrastructure.Tests.Analytics.Support;

namespace LoDb.Infrastructure.Tests.Analytics.Classification;

/// <summary>
/// The user agents of the sample are read as the legacy <c>UserAgentParser</c> read them:
/// robots first, then browser, system and device.
/// </summary>
public sealed class UserAgentParserTests
{
    [Fact]
    public void EveryUserAgentReadsAsInTheLegacyStack()
    {
        using var sample = LegacySamples.Json("classify.json");
        var cases = sample.RootElement.GetProperty("userAgents").EnumerateArray().ToList();

        var mismatches = new List<string>();
        foreach (var expected in cases)
        {
            var userAgent = LegacySamples.Text(expected, "ua");
            var profile = new UserAgentProfile(
                LegacySamples.Text(expected, "browser")!,
                LegacySamples.Text(expected, "os")!,
                LegacySamples.Text(expected, "device")!,
                expected.GetProperty("bot").GetBoolean());
            var actual = UserAgentParser.Parse(userAgent);
            if (actual != profile)
            {
                mismatches.Add($"{userAgent}: {actual} for {profile}");
            }
        }

        Assert.True(cases.Count >= 30);
        Assert.Empty(mismatches);
    }

    [Fact]
    public void RobotMarkerWinsOverAnyBrowser()
    {
        var profile = UserAgentParser.Parse(
            "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/120");

        Assert.Equal(UserAgentProfile.Robot, profile);
    }

    [Fact]
    public void MissingUserAgentIsUnidentified()
    {
        Assert.Equal(UserAgentProfile.Unidentified, UserAgentParser.Parse(null));
        Assert.Equal(UserAgentProfile.Unidentified, UserAgentParser.Parse("  "));
    }
}
