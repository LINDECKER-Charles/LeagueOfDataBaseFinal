using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Velopack;

namespace LoDb.Desktop.Updates.Engine.Signing;

/// <summary>
/// The trust the update sources place in release feeds (ADR 0008). A feed is read only when
/// its signature holds against the key the app trusts and every entry carries the size and
/// SHA-256 of its package; a refused feed counts as absent, so the app keeps its version. A
/// downloaded package is then accepted only when it matches the entry of a signed feed: what
/// authenticates a package, whether or not its platform signature is checked.
/// </summary>
internal sealed partial class SignedFeeds(FeedSignature signature, ILogger<SignedFeeds> logger)
{
    private const int Sha256HexLength = 64;
    private const char ByteOrderMark = '\uFEFF';

    // File name of a package -> its entry in a signed feed. Bounded by the releases read.
    private readonly ConcurrentDictionary<string, VelopackAsset> _signed =
        new(StringComparer.OrdinalIgnoreCase);

    /// <returns>The feed's entries; none when it is refused, which is logged.</returns>
    public IReadOnlyList<VelopackAsset> Accept(FeedFile feed)
    {
        if (feed.Signature is null)
        {
            return Refuse(feed, FeedRefusal.MissingSignature);
        }

        if (!signature.Holds(feed.Name, feed.Content, feed.Signature))
        {
            return Refuse(feed, FeedRefusal.InvalidSignature);
        }

        var assets = Parse(feed.Content);
        if (assets is null)
        {
            return Refuse(feed, FeedRefusal.Unreadable);
        }

        if (!assets.All(IsComplete))
        {
            return Refuse(feed, FeedRefusal.IncompleteEntry);
        }

        foreach (var asset in assets)
        {
            _signed[asset.FileName] = asset;
        }

        return assets;
    }

    /// <summary>
    /// Checks a downloaded package against the size and SHA-256 its signed feed lists. A
    /// refused file is never moved into place, and Velopack removes it with its partials.
    /// </summary>
    /// <exception cref="UnverifiedPackageException">
    /// No signed feed vouches for the file as downloaded.
    /// </exception>
    public async Task CheckPackageAsync(
        VelopackAsset package,
        string path,
        CancellationToken cancellationToken)
    {
        var mismatch = await FindMismatchAsync(package.FileName, path, cancellationToken);
        if (mismatch is null)
        {
            return;
        }

        LogPackageRefused(logger, package.FileName, mismatch);
        throw new UnverifiedPackageException($"{package.FileName}: {mismatch}");
    }

    private async Task<string?> FindMismatchAsync(
        string fileName,
        string path,
        CancellationToken cancellationToken)
    {
        if (!_signed.TryGetValue(fileName, out var entry))
        {
            return "no signed feed lists it";
        }

        if (new FileInfo(path).Length != entry.Size)
        {
            return "its size differs from the signed feed";
        }

        await using var content = File.OpenRead(path);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken));
        return hash.Equals(entry.SHA256, StringComparison.OrdinalIgnoreCase)
            ? null
            : "its SHA-256 differs from the signed feed";
    }

    private VelopackAsset[] Refuse(FeedFile feed, FeedRefusal refusal)
    {
        LogFeedRefused(logger, feed.Name, feed.Origin, refusal);
        return [];
    }

    private static VelopackAsset[]? Parse(byte[] content)
    {
        var json = Encoding.UTF8.GetString(content).TrimStart(ByteOrderMark);
        try
        {
            return VelopackAssetFeed.FromJson(json).Assets;
        }
        catch (Exception exception) when (exception is JsonException or FormatException
            or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    // Without a SHA-256, Velopack would fall back to the SHA-1 of the entry. A file name is
    // never a path: the local source joins it to its folder.
    private static bool IsComplete(VelopackAsset? asset) =>
        asset is { Size: > 0, SHA256.Length: Sha256HexLength, FileName: { } name }
        && Path.GetFileName(name) == name;

    [LoggerMessage(
        EventName = "desktop.update.feed_refused",
        Level = LogLevel.Warning,
        Message = "The update feed {FeedName} of {Origin} is ignored ({Refusal}).")]
    private static partial void LogFeedRefused(
        ILogger logger,
        string feedName,
        string origin,
        FeedRefusal refusal);

    [LoggerMessage(
        EventName = "desktop.update.package_refused",
        Level = LogLevel.Warning,
        Message = "The downloaded package {FileName} is refused: {Mismatch}.")]
    private static partial void LogPackageRefused(
        ILogger logger,
        string fileName,
        string mismatch);
}
