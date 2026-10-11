using LoDb.Desktop.Launch;
using LoDb.Desktop.Tests.Updates.Signing;
using LoDb.Desktop.Updates;
using LoDb.Desktop.Updates.Engine;
using LoDb.Desktop.Updates.Engine.Signing;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoDb.Desktop.Tests.Updates;

public sealed class UpdateFeedsTests
{
    private static readonly UpdateSettings Background = new() { Mode = UpdateMode.Background };

    private static readonly SignedFeeds Feeds = new(
        new TestFeedKey().Verifier(),
        NullLogger<SignedFeeds>.Instance);

    [Fact]
    public void ReadsTheStableReleasesOfTheRepositoryWithoutPreReleases()
    {
        var feed = Assert.IsType<SignedGithubSource>(
            UpdateFeeds.Create(Background, "stable", Feeds));

        Assert.Equal(new Uri(UpdateFeeds.Repository), feed.RepoUri);
        Assert.False(feed.Prerelease);
    }

    [Fact]
    public void ReadsThePreReleasesOnTheBetaChannel()
    {
        var feed = Assert.IsType<SignedGithubSource>(UpdateFeeds.Create(Background, "beta", Feeds));

        Assert.True(feed.Prerelease);
    }

    [Fact]
    public void ReadsALocalFolderGivenAsAnAbsolutePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "releases");

        var feed = UpdateFeeds.Create(Background with { LocalFeed = folder }, "stable", Feeds);

        Assert.Equal(folder, Assert.IsType<SignedFolderSource>(feed).Folder.FullName);
    }

    [Theory]
    [InlineData("releases")]
    [InlineData("https://example.com/releases")]
    [InlineData("file:///tmp/releases")]
    public void RefusesALocalFeedThatIsNotAnAbsoluteFolderPath(string value)
    {
        Assert.Null(UpdateFeeds.Create(Background with { LocalFeed = value }, "stable", Feeds));
    }

    [Fact]
    public void TrustsTheCompiledKeysUnlessALocalE2EBuildSetsItsOwn()
    {
        var release = new DesktopBuild { Version = "1.4.0", Channel = "stable" };
        var localE2E = release with { FeedKey = new TestFeedKey().PublicKey };

        Assert.Equal(UpdateFeeds.FeedPublicKeys, UpdateFeeds.TrustedKeys(release));
        Assert.Equal([localE2E.FeedKey], UpdateFeeds.TrustedKeys(localE2E));
    }

    [Fact]
    public void ReleaseBuildsSetNoFeedKeyOfTheirOwn()
    {
        Assert.Null(DesktopBuild.Of(typeof(UpdateFeeds).Assembly).FeedKey);
    }
}
