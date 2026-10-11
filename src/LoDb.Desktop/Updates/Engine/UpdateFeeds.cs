using System.Collections.Immutable;
using LoDb.Desktop.Launch;
using LoDb.Desktop.Updates.Engine.Signing;
using Velopack.Sources;

namespace LoDb.Desktop.Updates.Engine;

/// <summary>
/// Where the app reads its releases (ADR 0008): the GitHub Releases of the public repository,
/// or a local folder for the update E2E, either one through its signed feeds only. The
/// Velopack channel ({rid}, or {rid}-beta) is the one the package was packed with.
/// </summary>
internal static class UpdateFeeds
{
    /// <summary>The repository the release workflow publishes to.</summary>
    public const string Repository = "https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal";

    /// <summary>
    /// The Ed25519 public keys of the release feeds, base64 of their 32 raw bytes: the app
    /// reads no feed whose signature holds against none of them. One key, whose private half
    /// is the release secret <c>LODB_DESKTOP_UPDATE_PRIVATE_KEY</c>; two only while a key is
    /// rotated, the old one still signing the release that brings the new one. The release
    /// tools read this list here (tools/desktop-update-signing), one key per line: a release
    /// signed with a key it does not hold stops before it is published.
    /// </summary>
    public static readonly ImmutableArray<string> FeedPublicKeys =
    [
        "5kthbfZlQuxpzc4bLzvpi0hAq/2i/g9Zocih4rzggTs=",
    ];

    /// <summary>
    /// The keys a build trusts: the compiled ones, unless a local E2E build set its own.
    /// </summary>
    public static ImmutableArray<string> TrustedKeys(DesktopBuild build) =>
        build.FeedKey is { } key ? [key] : FeedPublicKeys;

    /// <param name="settings">The update settings of the run.</param>
    /// <param name="channel">The build's release channel (<c>stable</c> or <c>beta</c>).</param>
    /// <param name="feeds">The trust placed in the feeds and in the packages they list.</param>
    /// <returns>Null when the local feed is not an absolute folder path.</returns>
    public static IUpdateSource? Create(
        UpdateSettings settings,
        string channel,
        SignedFeeds feeds) =>
        settings.LocalFeed is null ? Github(channel, feeds) : Local(settings.LocalFeed, feeds);

    // A stable build never sees a pre-release: a release is promoted once every RID is up,
    // and beta releases stay pre-releases. Anonymous: the repository is public.
    private static SignedGithubSource Github(string channel, SignedFeeds feeds) =>
        new(prerelease: channel == DesktopChannels.Beta, feeds);

    // A path only: a URL here would let the environment swap the feed for a remote one.
    private static SignedFolderSource? Local(string folder, SignedFeeds feeds) =>
        Path.IsPathFullyQualified(folder)
            ? new SignedFolderSource(new DirectoryInfo(folder), feeds)
            : null;
}
