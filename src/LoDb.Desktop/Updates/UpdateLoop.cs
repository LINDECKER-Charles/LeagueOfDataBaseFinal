namespace LoDb.Desktop.Updates;

/// <summary>
/// When the app looks for updates (ADR 0008): at start and every six hours for the window,
/// never for a plain smoke check, once and awaited for the update E2E. As the host stops, a
/// ready update is handed to the updater: the browser fallback and a signal end the app
/// without the window's closing handler.
/// </summary>
internal sealed partial class UpdateLoop(
    VelopackUpdates updates,
    UpdateSettings settings,
    TimeProvider time,
    ILogger<UpdateLoop> logger) : BackgroundService
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    /// <summary>Bound on the awaited check of the E2E, download included.</summary>
    public static readonly TimeSpan AwaitedCheckTimeout = TimeSpan.FromMinutes(10);

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (settings.Mode == UpdateMode.ApplyAtExit)
        {
            // Awaited before the host serves: the smoke report then shows the result.
            await CheckWithinTimeoutAsync(cancellationToken);
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        updates.PrepareExit();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (settings.Mode != UpdateMode.Background)
        {
            return;
        }

        using var timer = new PeriodicTimer(CheckInterval, time);
        do
        {
            await updates.CheckAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CheckWithinTimeoutAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(AwaitedCheckTimeout, time);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        try
        {
            await updates.CheckAsync(bounded.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogCheckTimedOut(logger, AwaitedCheckTimeout);
        }
    }

    [LoggerMessage(
        EventName = "desktop.update.check_timed_out",
        Level = LogLevel.Warning,
        Message = "The awaited update check did not end within {Timeout}.")]
    private static partial void LogCheckTimedOut(ILogger logger, TimeSpan timeout);
}
