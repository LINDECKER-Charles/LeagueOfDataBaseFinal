using System.Text;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace LoDb.Desktop.Updates.Engine.Signing;

/// <summary>
/// The GitHub Releases of the repository (ADR 0008), read as <see cref="GithubSource"/> reads
/// them, but each release's feed only through <see cref="SignedFeeds"/>: a release whose
/// <c>releases.{channel}.json.sig</c> is missing or does not hold counts as having no feed,
/// and a downloaded package must match its signed entry.
/// </summary>
/// <param name="prerelease">True on the beta channel, which also reads the pre-releases.</param>
/// <param name="feeds">The trust placed in the feeds and the packages they list.</param>
/// <param name="downloader">The HTTP client of the tests; Velopack's own otherwise.</param>
internal sealed class SignedGithubSource(
    bool prerelease,
    SignedFeeds feeds,
    IFileDownloader? downloader = null)
    : GithubSource(UpdateFeeds.Repository, accessToken: null, prerelease, downloader)
{
    private const string OctetStream = "application/octet-stream";

    public override async Task<VelopackAssetFeed> GetReleaseFeed(
        IVelopackLogger logger,
        string? appId,
        string channel,
        Guid? stagingId = null,
        VelopackAsset? latestLocalRelease = null)
    {
        var feedName = FeedFile.NameFor(channel);
        var assets = new List<VelopackAsset>();
        foreach (var release in await GetReleases(Prerelease))
        {
            if (await ReadFeedAsync(release, feedName) is { } feed)
            {
                assets.AddRange(
                    feeds.Accept(feed).Select(asset => new GitBaseAsset(asset, release)));
            }
        }

        return new VelopackAssetFeed { Assets = [.. assets] };
    }

    public override async Task DownloadReleaseEntry(
        IVelopackLogger logger,
        VelopackAsset releaseEntry,
        string localFile,
        Action<int> progress,
        CancellationToken cancelToken)
    {
        await base.DownloadReleaseEntry(logger, releaseEntry, localFile, progress, cancelToken);
        await feeds.CheckPackageAsync(releaseEntry, localFile, cancelToken);
    }

    // A release without this channel's feed (another app, an Android release) is skipped, as
    // Velopack does.
    private async Task<FeedFile?> ReadFeedAsync(GithubRelease release, string feedName)
    {
        if (!HasAsset(release, feedName))
        {
            return null;
        }

        var signatureName = feedName + FeedFile.SignatureSuffix;
        return new FeedFile
        {
            Name = feedName,
            Origin = release.Name ?? string.Empty,
            Content = await DownloadAsync(release, feedName),
            Signature = HasAsset(release, signatureName)
                ? Encoding.UTF8.GetString(await DownloadAsync(release, signatureName))
                : null,
        };
    }

    // GetAssetUrlFromName throws when the asset is missing, and matches names without case.
    private static bool HasAsset(GithubRelease release, string name) =>
        (release.Assets ?? []).Any(
            asset => string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase));

    private Task<byte[]> DownloadAsync(GithubRelease release, string assetName) =>
        Downloader.DownloadBytes(
            GetAssetUrlFromName(release, assetName),
            GetRequestHeaders(OctetStream));
}
