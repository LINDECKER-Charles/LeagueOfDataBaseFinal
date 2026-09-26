namespace LoDb.Desktop.Shell;

/// <summary>
/// The native window and its WebView (ADR 0007). Only its implementation references the
/// windowing library, so that the plan B (Avalonia) replaces that implementation alone.
/// </summary>
internal interface IDesktopShell
{
    /// <summary>
    /// A JavaScript expression evaluating to <c>{send(message), listen(listener)}</c> over
    /// the shell's native messaging: the host hands it to the page in the desktop marker.
    /// </summary>
    string BridgeTransport { get; }

    /// <summary>Shows the window, and blocks until it closes.</summary>
    /// <exception cref="Exception">
    /// The WebView cannot start (missing runtime or library).
    /// </exception>
    void Run(ShellWindow window);

    /// <summary>
    /// Closes the window from outside, when the host stops on a signal; the closing handler
    /// runs as for the user's click. Callable from any thread, it waits for the UI thread:
    /// never call it holding a lock the closing handler takes.
    /// </summary>
    void Close();

    /// <summary>Sends a message to the page; callable from any thread.</summary>
    void Post(string message);

    /// <summary>The native save dialog; the chosen path, or null when cancelled.</summary>
    Task<string?> PickSaveFileAsync(string suggestedName, CancellationToken cancellationToken);
}
