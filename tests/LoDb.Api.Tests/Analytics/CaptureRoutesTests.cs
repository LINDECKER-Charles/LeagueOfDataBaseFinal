using LoDb.Api.Modules.Analytics.Capture;

namespace LoDb.Api.Tests.Analytics;

/// <summary>
/// The target a page view names reads as a path of this site: rooted, not a network path;
/// its grammar is checked later, off the request.
/// </summary>
public sealed class CaptureRoutesTests
{
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
    public void AnythingElseIsNoTarget(string? target) =>
        Assert.False(CaptureRoutes.IsTarget(target));
}
