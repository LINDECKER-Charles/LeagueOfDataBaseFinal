namespace LoDb.Desktop.Lifecycle;

/// <summary>
/// The app's updates, as the bridge and the window see them. L9.4 implements it with
/// Velopack under <c>Updates/</c> and registers it in <c>DesktopServices</c>; until then
/// <see cref="NoDesktopUpdates"/> answers.
/// </summary>
internal interface IDesktopUpdates
{
    /// <summary>Read by the bridge's <c>updateState</c>; cheap and never blocking.</summary>
    UpdateSnapshot Current { get; }

    /// <summary>
    /// Applies the downloaded update and restarts the app (the bridge's <c>applyUpdate</c>),
    /// so the reply may never reach the page. False when no update is ready.
    /// </summary>
    Task<bool> ApplyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called from the window's closing handler, the last code that runs on macOS (spike
    /// report, 4.1): where an update ready at exit is handed over to Velopack
    /// (<c>WaitExitThenApplyUpdates</c>). Must be quick and must not throw.
    /// </summary>
    void PrepareExit();
}
