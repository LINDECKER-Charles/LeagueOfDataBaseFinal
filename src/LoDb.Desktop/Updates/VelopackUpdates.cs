using LoDb.Desktop.Lifecycle;
using LoDb.Desktop.Updates.Engine;
using Velopack;

namespace LoDb.Desktop.Updates;

/// <summary>
/// <see cref="IDesktopUpdates"/> over Velopack (ADR 0008): a newer version is downloaded in
/// the background, then applied on the user's request, with a restart, or silently as the
/// app exits. The ready release is handed to the updater once, whichever comes first.
/// </summary>
internal sealed partial class VelopackUpdates(
    IUpdateEngine engine,
    IHostApplicationLifetime lifetime,
    ILogger<VelopackUpdates> logger) : IDesktopUpdates
{
    private readonly Lock _gate = new();
    private UpdateSnapshot _current = UpdateSnapshot.None;
    private VelopackAsset? _ready;
    private bool _isHandedOver;

    public UpdateSnapshot Current => Volatile.Read(ref _current);

    /// <summary>
    /// Looks for a newer version and downloads it. A failure is logged and leaves the state
    /// as it was, for the next check to try again; only the cancellation of
    /// <paramref name="cancellationToken"/> is thrown.
    /// </summary>
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        if (!engine.IsInstalled || IsHandedOver())
        {
            return;
        }

        AdoptPending();
        var update = await FindNewerAsync(cancellationToken);
        if (update is not null && !IsReady(update.TargetFullRelease))
        {
            await DownloadAsync(update, cancellationToken);
        }
    }

    /// <remarks>
    /// The updater waits for the process to end: stopping the host closes the window, as the
    /// user's click would, then the updater applies the release and restarts the app.
    /// </remarks>
    public Task<bool> ApplyAsync(CancellationToken cancellationToken)
    {
        if (!HandOver(engine.RestartInto))
        {
            return Task.FromResult(false);
        }

        lifetime.StopApplication();
        return Task.FromResult(true);
    }

    public void PrepareExit() => HandOver(engine.ApplyAtExit);

    private bool IsHandedOver()
    {
        lock (_gate)
        {
            return _isHandedOver;
        }
    }

    private bool IsReady(VelopackAsset release)
    {
        lock (_gate)
        {
            return _ready is not null && _ready.Version == release.Version;
        }
    }

    // A release downloaded by an earlier run that exited without applying it (killed).
    private void AdoptPending()
    {
        lock (_gate)
        {
            if (_ready is not null)
            {
                return;
            }
        }

        if (engine.Pending is { } pending)
        {
            MarkReady(pending);
        }
    }

    private async Task<UpdateInfo?> FindNewerAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await engine.FindNewerAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Offline, rate-limited, or an HTTP timeout: transient, the next check retries.
            LogCheckFailed(logger, exception);
            return null;
        }
    }

    private async Task DownloadAsync(UpdateInfo update, CancellationToken cancellationToken)
    {
        var release = update.TargetFullRelease;
        var version = release.Version.ToString();
        var before = Current;
        Publish(new UpdateSnapshot { Stage = UpdateStage.Downloading, Version = version });
        try
        {
            await engine.DownloadAsync(update, cancellationToken);
        }
        catch (Exception exception)
        {
            Publish(before);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            LogDownloadFailed(logger, release.Version, exception);
            return;
        }

        MarkReady(release);
        LogDownloaded(logger, release.Version, update.DeltasToTarget?.Length ?? 0);
    }

    private void MarkReady(VelopackAsset release)
    {
        lock (_gate)
        {
            _ready = release;
            Publish(new UpdateSnapshot
            {
                Stage = UpdateStage.Ready,
                Version = release.Version.ToString(),
            });
        }
    }

    private void Publish(UpdateSnapshot snapshot) => Volatile.Write(ref _current, snapshot);

    // Never throws: the closing handler calls it, and nothing runs after it on macOS.
    private bool HandOver(Action<VelopackAsset> startUpdater)
    {
        VelopackAsset release;
        lock (_gate)
        {
            if (_isHandedOver || _ready is null)
            {
                return false;
            }

            release = _ready;
            _isHandedOver = true;
        }

        try
        {
            startUpdater(release);
            LogHandedOver(logger, release.Version);
            return true;
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                _isHandedOver = false;
            }

            LogHandOverFailed(logger, release.Version, exception);
            return false;
        }
    }

    [LoggerMessage(
        EventName = "desktop.update.check_failed",
        Level = LogLevel.Warning,
        Message = "The update feed could not be read; the next check tries again.")]
    private static partial void LogCheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventName = "desktop.update.download_failed",
        Level = LogLevel.Warning,
        Message = "Version {Version} could not be downloaded; the next check tries again.")]
    private static partial void LogDownloadFailed(
        ILogger logger,
        SemanticVersion version,
        Exception exception);

    [LoggerMessage(
        EventName = "desktop.update.downloaded",
        Level = LogLevel.Information,
        Message = "Version {Version} is downloaded ({DeltaCount} deltas) and ready to apply.")]
    private static partial void LogDownloaded(
        ILogger logger,
        SemanticVersion version,
        int deltaCount);

    [LoggerMessage(
        EventName = "desktop.update.handed_over",
        Level = LogLevel.Information,
        Message = "Version {Version} is handed to the updater, which applies it at exit.")]
    private static partial void LogHandedOver(ILogger logger, SemanticVersion version);

    [LoggerMessage(
        EventName = "desktop.update.handover_failed",
        Level = LogLevel.Error,
        Message = "The updater could not be started for version {Version}.")]
    private static partial void LogHandOverFailed(
        ILogger logger,
        SemanticVersion version,
        Exception exception);
}
