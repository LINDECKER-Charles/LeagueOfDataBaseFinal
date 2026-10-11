using LoDb.Desktop.Tests.Support;
using LoDb.Desktop.Updates.Engine.Signing;
using Microsoft.Extensions.Logging.Abstractions;
using Velopack;
using Velopack.Logging;

namespace LoDb.Desktop.Tests.Updates.Signing;

/// <summary>The local release folder of the update gate, held to the signed feeds.</summary>
public sealed class SignedFolderSourceTests : IDisposable
{
    private readonly TestFeedKey _key = new();
    private readonly TestFolder _releases = new();
    private readonly TestFolder _downloads = new();
    private readonly SignedFolderSource _source;

    public SignedFolderSourceTests() => _source = new SignedFolderSource(
        new DirectoryInfo(_releases.Path),
        new SignedFeeds(_key.Verifier(), NullLogger<SignedFeeds>.Instance));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _releases.Dispose();
        _downloads.Dispose();
    }

    [Fact]
    public async Task ReadsASignedFeedAndCopiesItsPackage()
    {
        var feed = TestReleases.Feed("1.4.0");
        WriteRelease("1.4.0", feed, _key.Sign(feed));

        var asset = Assert.Single(await ReadFeedAsync());
        var path = await CopyAsync(asset);

        Assert.Equal(TestReleases.Package("1.4.0"), await File.ReadAllBytesAsync(path, Token));
    }

    [Fact]
    public async Task FindsNothingInAnUnsignedFolder()
    {
        WriteRelease("1.4.0", TestReleases.Feed("1.4.0"), signature: null);

        Assert.Empty(await ReadFeedAsync());
    }

    // SimpleFileSource rebuilds a feed from the packages when there is none.
    [Fact]
    public async Task NeverListsThePackagesOfAFolderWithoutFeed()
    {
        File.WriteAllBytes(_releases.Combine(TestReleases.FileName("1.4.0")), [1, 2, 3]);

        Assert.Empty(await ReadFeedAsync());
    }

    [Fact]
    public async Task RefusesAPackageSwappedAfterTheFeedWasSigned()
    {
        var feed = TestReleases.Feed("1.4.0");
        WriteRelease("1.4.0", feed, _key.Sign(feed));
        File.WriteAllBytes(
            _releases.Combine(TestReleases.FileName("1.4.0")),
            TestReleases.Package("6.6.6"));
        var asset = Assert.Single(await ReadFeedAsync());

        await Assert.ThrowsAsync<UnverifiedPackageException>(() => CopyAsync(asset));
    }

    private async Task<VelopackAsset[]> ReadFeedAsync()
    {
        var feed = await _source.GetReleaseFeed(
            NullVelopackLogger.Instance,
            "LoDb.Desktop",
            TestReleases.Channel);
        return feed.Assets;
    }

    private async Task<string> CopyAsync(VelopackAsset asset)
    {
        var path = _downloads.Combine("download.partial");
        await _source.DownloadReleaseEntry(
            NullVelopackLogger.Instance,
            asset,
            path,
            _ => { },
            Token);
        return path;
    }

    private void WriteRelease(string version, byte[] feed, string? signature)
    {
        File.WriteAllBytes(_releases.Combine(TestReleases.FeedName), feed);
        File.WriteAllBytes(
            _releases.Combine(TestReleases.FileName(version)),
            TestReleases.Package(version));
        if (signature is not null)
        {
            var signaturePath = TestReleases.FeedName + FeedFile.SignatureSuffix;
            File.WriteAllText(_releases.Combine(signaturePath), signature);
        }
    }
}
