using LoDb.Desktop.Launch;
using Velopack.Sources;

namespace LoDb.Desktop.Updates.Engine;

/// <summary>
/// Where the app reads its releases (ADR 0008): the GitHub Releases of the public repository,
/// or a local folder for the update E2E. The Velopack channel ({rid}, or {rid}-beta) is the
/// one the package was packed with.
/// </summary>
internal static class UpdateFeeds
{
    /// <summary>The repository the release workflow publishes to.</summary>
    public const string Repository = "https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal";

    /// <param name="settings">The update settings of the run.</param>
    /// <param name="channel">The build's release channel (<c>stable</c> or <c>beta</c>).</param>
    /// <returns>Null when the local feed is not an absolute folder path.</returns>
    public static IUpdateSource? Create(UpdateSettings settings, string channel) =>
        settings.LocalFeed is null ? Github(channel) : Local(settings.LocalFeed);

    // A stable build never sees a pre-release: a release is promoted once every RID is up,
    // and beta releases stay pre-releases. Anonymous: the repository is public.
    private static GithubSource Github(string channel) => new(
        Repository,
        accessToken: null,
        prerelease: channel == DesktopChannels.Beta,
        downloader: null);

    // A path only: a URL here would let the environment swap the feed for a remote one.
    private static SimpleFileSource? Local(string folder) =>
        Path.IsPathFullyQualified(folder) ? new SimpleFileSource(new DirectoryInfo(folder)) : null;
}
