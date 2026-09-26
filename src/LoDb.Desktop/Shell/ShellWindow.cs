namespace LoDb.Desktop.Shell;

/// <summary>What the host asks of the window it runs.</summary>
internal sealed record ShellWindow
{
    /// <summary>The loopback URL the WebView loads. Never a custom scheme (ADR 0007).</summary>
    public required Uri Address { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// The WebView profile folder, inside the app's data folder: never the install folder
    /// that Velopack replaces, nor a folder shared with other apps (spike report, rule 6).
    /// </summary>
    public required string WebViewDirectory { get; init; }

    public required bool IsDevToolsEnabled { get; init; }

    /// <summary>Called off the UI thread with each message of the page.</summary>
    public required Func<string, Task> OnMessage { get; init; }

    /// <summary>
    /// Called on the UI thread as the window closes. On macOS the process ends right after
    /// it returns, so all the end-of-life work belongs here (spike report, 4.1).
    /// </summary>
    public required Action OnClosing { get; init; }
}
