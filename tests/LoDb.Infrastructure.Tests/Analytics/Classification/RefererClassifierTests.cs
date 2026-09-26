using LoDb.Infrastructure.Analytics.Classification;
using LoDb.Infrastructure.Tests.Analytics.Support;

namespace LoDb.Infrastructure.Tests.Analytics.Classification;

/// <summary>
/// The referrers of the sample are classified as the legacy <c>RefererClassifier</c> did:
/// the site and its subdomains, then search engines, social networks, anything else.
/// </summary>
public sealed class RefererClassifierTests
{
    [Fact]
    public void EveryReferrerIsClassifiedAsInTheLegacyStack()
    {
        using var sample = LegacySamples.Json("classify.json");
        var appHost = sample.RootElement.GetProperty("appHost").GetString()!;
        var cases = sample.RootElement.GetProperty("referers").EnumerateArray().ToList();

        var mismatches = cases
            .Select(expected => (
                Referer: LegacySamples.Text(expected, "referer"),
                Expected: new RefererOrigin(
                    LegacySamples.Text(expected, "host"),
                    LegacySamples.Text(expected, "source")!),
                Actual: RefererClassifier.Classify(
                    LegacySamples.Text(expected, "referer"),
                    appHost)))
            .Where(static item => item.Expected != item.Actual)
            .Select(static item => $"{item.Referer}: {item.Actual} for {item.Expected}")
            .ToList();

        Assert.True(cases.Count >= 20);
        Assert.Empty(mismatches);
    }

    [Theory]
    [InlineData("https://league-of-data-base.com/fr/items", RefererSources.Internal)]
    [InlineData("https://api.league-of-data-base.com/v1/", RefererSources.Internal)]
    [InlineData("https://www.google.fr/", RefererSources.Search)]
    [InlineData("https://www.reddit.com/r/leagueoflegends", RefererSources.Social)]
    [InlineData("https://example.org/", RefererSources.External)]
    [InlineData("not a url", RefererSources.Direct)]
    public void SourceFollowsTheHost(string referer, string source)
    {
        Assert.Equal(
            source,
            RefererClassifier.Classify(referer, "League-Of-Data-Base.com").Source);
    }
}
