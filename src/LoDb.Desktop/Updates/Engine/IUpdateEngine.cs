using Velopack;

namespace LoDb.Desktop.Updates.Engine;

/// <summary>
/// The part of Velopack the app uses: find, download, and hand a release over to the
/// updater, which replaces the app once its process has exited.
/// </summary>
internal interface IUpdateEngine
{
    /// <summary>False for a build Velopack did not install (development, tests).</summary>
    bool IsInstalled { get; }

    /// <summary>A release downloaded by an earlier run and never applied; null when none.</summary>
    VelopackAsset? Pending { get; }

    /// <summary>The newest release of the feed above the running one; null when none.</summary>
    /// <exception cref="Exception">The feed cannot be read (offline, rate limit).</exception>
    Task<UpdateInfo?> FindNewerAsync(CancellationToken cancellationToken);

    /// <summary>Downloads the release, from its deltas when the feed has them.</summary>
    /// <exception cref="Exception">The download or its checksum fails.</exception>
    Task DownloadAsync(UpdateInfo update, CancellationToken cancellationToken);

    /// <summary>Starts the updater: it applies the release silently once the app exits.</summary>
    void ApplyAtExit(VelopackAsset release);

    /// <summary>
    /// Starts the updater: it applies the release once the app exits, then restarts it.
    /// </summary>
    void RestartInto(VelopackAsset release);
}
