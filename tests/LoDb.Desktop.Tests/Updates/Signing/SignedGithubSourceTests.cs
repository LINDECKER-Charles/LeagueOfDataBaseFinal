using System.Text;
using LoDb.Desktop.Tests.Support;
using LoDb.Desktop.Updates.Engine.Signing;
using Microsoft.Extensions.Logging.Abstractions;
using Velopack;
using Velopack.Logging;

namespace LoDb.Desktop.Tests.Updates.Signing;

/// <summary>
/// The GitHub source over releases signed, unsigned and tampered with: the app finds only
/// what a signed feed lists, and takes only the package that feed names.
/// </summary>
public sealed class SignedGithubSourceTests : IDisposable
{
    private readonly TestFeedKey _key = new();
    private readonly FakeGithub _github = new();
    private readonly TestFolder _folder = new();
    private readonly SignedGithubSource _source;

    public SignedGithubSourceTests() => _source = new SignedGithubSource(
        prerelease: false,
        new SignedFeeds(_key.Verifier(), NullLogger<SignedFeeds>.Instance),
        _github);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task FindsTheReleasesOfSignedFeedsOnly()
    {
        PublishUnsigned("1.3.0");
        PublishSigned("1.4.0");
        PublishTampered("1.5.0");

        var versions = (await ReadFeedAsync()).Select(asset => asset.Version.ToString());

        Assert.Equal(["1.4.0"], versions);
    }

    [Fact]
    public async Task SkipsAReleaseWithoutTheFeedOfTheChannel()
    {
        PublishSigned("1.4.0");
        _github.Publish("android-v2.4.0", new Dictionary<string, byte[]>
        {
            ["lodb-bundle-2.4.0.zip"] = [1, 2, 3],
        });

        var asset = Assert.Single(await ReadFeedAsync());

        Assert.Equal("1.4.0", asset.Version.ToString());
        Assert.DoesNotContain(
            _github.Downloads,
            url => url.Contains("android", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DownloadsThePackageItsSignedFeedNames()
    {
        PublishSigned("1.4.0");
        var asset = Assert.Single(await ReadFeedAsync());

        var path = await DownloadAsync(asset);

        Assert.Equal(TestReleases.Package("1.4.0"), await File.ReadAllBytesAsync(path, Token));
    }

    [Fact]
    public async Task RefusesAPackageSwappedAfterTheFeedWasSigned()
    {
        var feed = TestReleases.Feed("1.4.0");
        var files = Files("1.4.0", feed, _key.Sign(feed));
        files[TestReleases.FileName("1.4.0")] = Encoding.UTF8.GetBytes("package 6.6.6");
        _github.Publish("desktop-v1.4.0", files);
        var asset = Assert.Single(await ReadFeedAsync());

        await Assert.ThrowsAsync<UnverifiedPackageException>(() => DownloadAsync(asset));
    }

    private async Task<VelopackAsset[]> ReadFeedAsync()
    {
        var feed = await _source.GetReleaseFeed(
            NullVelopackLogger.Instance,
            "LoDb.Desktop",
            TestReleases.Channel);
        return feed.Assets;
    }

    private async Task<string> DownloadAsync(VelopackAsset asset)
    {
        var path = _folder.Combine("download.partial");
        await _source.DownloadReleaseEntry(
            NullVelopackLogger.Instance,
            asset,
            path,
            _ => { },
            Token);
        return path;
    }

    private void PublishSigned(string version)
    {
        var feed = TestReleases.Feed(version);
        Publish(version, feed, _key.Sign(feed));
    }

    private void PublishUnsigned(string version) =>
        Publish(version, TestReleases.Feed(version), signature: null);

    // Signed, but the signature is the one of another feed.
    private void PublishTampered(string version) =>
        Publish(version, TestReleases.Feed(version), _key.Sign(TestReleases.Feed("1.4.0")));

    private void Publish(string version, byte[] feed, string? signature) =>
        _github.Publish($"desktop-v{version}", Files(version, feed, signature));

    private static Dictionary<string, byte[]> Files(string version, byte[] feed, string? signature)
    {
        var files = new Dictionary<string, byte[]>
        {
            [TestReleases.FeedName] = feed,
            [TestReleases.FileName(version)] = TestReleases.Package(version),
        };
        if (signature is not null)
        {
            var signatureName = TestReleases.FeedName + FeedFile.SignatureSuffix;
            files[signatureName] = Encoding.UTF8.GetBytes(signature);
        }

        return files;
    }
}
