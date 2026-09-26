using LoDb.Desktop.Hosting;
using Velopack;

namespace LoDb.Desktop.Updates.Engine;

/// <summary>
/// <see cref="IUpdateEngine"/> over Velopack's <see cref="UpdateManager"/>, created on first
/// use. Without an install (development, tests) or with an invalid local feed, it reports
/// no install, and the app never looks for updates.
/// </summary>
internal sealed partial class VelopackEngine(
    UpdateSettings settings,
    DesktopOptions options,
    ILogger<VelopackEngine> logger) : IUpdateEngine
{
    private readonly Lazy<UpdateManager?> _manager = new(
        () => Create(settings, options.Channel, logger));

    public bool IsInstalled => _manager.Value?.IsInstalled == true;

    public VelopackAsset? Pending => IsInstalled ? _manager.Value!.UpdatePendingRestart : null;

    private UpdateManager Manager => IsInstalled
        ? _manager.Value!
        : throw new InvalidOperationException("Velopack did not install this build.");

    // Velopack's check takes no token: a cancelled check is abandoned, never awaited.
    public Task<UpdateInfo?> FindNewerAsync(CancellationToken cancellationToken) =>
        Manager.CheckForUpdatesAsync().WaitAsync(cancellationToken);

    public Task DownloadAsync(UpdateInfo update, CancellationToken cancellationToken) =>
        Manager.DownloadUpdatesAsync(update, progress: null, cancellationToken);

    public void ApplyAtExit(VelopackAsset release) =>
        Manager.WaitExitThenApplyUpdates(release, silent: true, restart: false);

    public void RestartInto(VelopackAsset release) =>
        Manager.WaitExitThenApplyUpdates(release, silent: false, restart: true);

    private static UpdateManager? Create(
        UpdateSettings settings,
        string channel,
        ILogger<VelopackEngine> logger)
    {
        var source = UpdateFeeds.Create(settings, channel);
        if (source is null)
        {
            LogInvalidLocalFeed(logger, UpdateArguments.LocalFeedVariable);
            return null;
        }

        try
        {
            return new UpdateManager(source);
        }
        catch (InvalidOperationException)
        {
            // No Velopack locator: VelopackApp did not start this process (tests).
            return null;
        }
    }

    [LoggerMessage(
        EventName = "desktop.update.feed_invalid",
        Level = LogLevel.Warning,
        Message = "{Variable} is not an absolute folder path: updates are off for this run.")]
    private static partial void LogInvalidLocalFeed(ILogger logger, string variable);
}
