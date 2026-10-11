using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace LoDb.Desktop.Updates.Engine.Signing;

/// <summary>
/// A local release folder (<c>LODB_DESKTOP_UPDATE_SOURCE</c>, the update gate), held to the
/// rule of GitHub Releases: its feed is read only through <see cref="SignedFeeds"/>, and never
/// rebuilt from the folder's packages, as <see cref="SimpleFileSource"/> does without one.
/// </summary>
internal sealed class SignedFolderSource(DirectoryInfo folder, SignedFeeds feeds) : IUpdateSource
{
    private readonly SimpleFileSource _files = new(folder);

    public DirectoryInfo Folder => folder;

    public async Task<VelopackAssetFeed> GetReleaseFeed(
        IVelopackLogger logger,
        string? appId,
        string channel,
        Guid? stagingId = null,
        VelopackAsset? latestLocalRelease = null)
    {
        var feedName = FeedFile.NameFor(channel);
        var feedPath = Path.Combine(folder.FullName, feedName);
        if (!File.Exists(feedPath))
        {
            return new VelopackAssetFeed();
        }

        var signaturePath = feedPath + FeedFile.SignatureSuffix;
        var feed = new FeedFile
        {
            Name = feedName,
            Origin = folder.FullName,
            Content = await File.ReadAllBytesAsync(feedPath),
            Signature = File.Exists(signaturePath)
                ? await File.ReadAllTextAsync(signaturePath)
                : null,
        };
        return new VelopackAssetFeed { Assets = [.. feeds.Accept(feed)] };
    }

    public async Task DownloadReleaseEntry(
        IVelopackLogger logger,
        VelopackAsset releaseEntry,
        string localFile,
        Action<int> progress,
        CancellationToken cancelToken = default)
    {
        await _files.DownloadReleaseEntry(logger, releaseEntry, localFile, progress, cancelToken);
        await feeds.CheckPackageAsync(releaseEntry, localFile, cancelToken);
    }
}
