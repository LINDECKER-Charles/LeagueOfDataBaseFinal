using LoDb.Desktop.Updates;
using LoDb.Desktop.Updates.Engine;
using Velopack.Sources;

namespace LoDb.Desktop.Tests.Updates;

public sealed class UpdateFeedsTests
{
    private static readonly UpdateSettings Background = new() { Mode = UpdateMode.Background };

    [Fact]
    public void ReadsTheStableReleasesOfTheRepositoryWithoutPreReleases()
    {
        var feed = Assert.IsType<GithubSource>(UpdateFeeds.Create(Background, "stable"));

        Assert.Equal(new Uri(UpdateFeeds.Repository), feed.RepoUri);
        Assert.False(feed.Prerelease);
    }

    [Fact]
    public void ReadsThePreReleasesOnTheBetaChannel()
    {
        var feed = Assert.IsType<GithubSource>(UpdateFeeds.Create(Background, "beta"));

        Assert.True(feed.Prerelease);
    }

    [Fact]
    public void ReadsALocalFolderGivenAsAnAbsolutePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "releases");

        var feed = UpdateFeeds.Create(Background with { LocalFeed = folder }, "stable");

        Assert.Equal(folder, Assert.IsType<SimpleFileSource>(feed).BaseDirectory.FullName);
    }

    [Theory]
    [InlineData("releases")]
    [InlineData("./releases")]
    [InlineData("https://example.com/releases")]
    [InlineData("file:///tmp/releases")]
    public void RefusesALocalFeedThatIsNotAnAbsoluteFolderPath(string value)
    {
        Assert.Null(UpdateFeeds.Create(Background with { LocalFeed = value }, "stable"));
    }
}
