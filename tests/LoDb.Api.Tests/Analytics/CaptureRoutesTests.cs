using LoDb.Api.Modules.Analytics.Capture;

namespace LoDb.Api.Tests.Analytics;

/// <summary>
/// The target a page view names reads as a path of this site: rooted, not a network path,
/// without control characters, 2048 characters at most; its grammar is checked later, off
/// the request.
/// </summary>
public sealed class CaptureRoutesTests
{
    private const int LongestTarget = 2048;

    [Theory]
    [InlineData("/")]
    [InlineData("/fr/champions/Ahri?lang=fr_FR")]
    public void PathOfTheSiteIsATarget(string target) =>
        Assert.True(CaptureRoutes.IsTarget(target));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr/champions")]
    [InlineData("//evil.example/fr")]
    [InlineData("/\\evil.example/fr")]
    [InlineData("/fr/\nchampions")]
    public void AnythingElseIsNoTarget(string? target) =>
        Assert.False(CaptureRoutes.IsTarget(target));

    [Fact]
    public void TargetIsBoundedTo2048Characters()
    {
        var longest = "/" + new string('a', LongestTarget - 1);

        Assert.True(CaptureRoutes.IsTarget(longest));
        Assert.False(CaptureRoutes.IsTarget(longest + "a"));
    }
}
