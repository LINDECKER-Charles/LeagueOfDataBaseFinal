using System.Collections.Concurrent;
using LoDb.Desktop.Shell;

namespace LoDb.Desktop.Tests.Support;

/// <summary>A window that never opens: records the posts and answers the save dialog.</summary>
internal sealed class FakeShell : IDesktopShell
{
    public const string Transport = "window.__testBridge";

    // Photino waits for its UI thread without bound; bounded here, so that a deadlock fails
    // the test instead of hanging the run.
    private static readonly TimeSpan UiThreadTimeout = TimeSpan.FromSeconds(5);

    private volatile bool _isClosed;

    public ConcurrentQueue<string> Posted { get; } = new();

    public List<string> SuggestedNames { get; } = [];

    /// <summary>The path the "user" picks; null cancels the dialog.</summary>
    public string? PickedPath { get; set; }

    /// <summary>Thrown by <see cref="Run"/> to simulate a WebView that cannot start.</summary>
    public Exception? StartFailure { get; set; }

    /// <summary>What happens while the window is open, before it returns.</summary>
    public Action<ShellWindow>? WhileOpen { get; set; }

    public ShellWindow? Window { get; private set; }

    /// <summary>
    /// True once <see cref="Close"/> has run the closing handler within the time a UI thread
    /// takes.
    /// </summary>
    public bool IsClosed => _isClosed;

    public string BridgeTransport => Transport;

    public void Run(ShellWindow window)
    {
        if (StartFailure is not null)
        {
            throw StartFailure;
        }

        Window = window;
        WhileOpen?.Invoke(window);
    }

    /// <remarks>
    /// As Photino's Invoke: the closing handler runs on another thread, the UI thread, and
    /// the caller waits for it.
    /// </remarks>
    public void Close()
    {
        var window = Window;
        var uiThread = new Thread(() => window?.OnClosing());
        uiThread.Start();
        _isClosed = uiThread.Join(UiThreadTimeout);
    }

    public void Post(string message) => Posted.Enqueue(message);

    public Task<string?> PickSaveFileAsync(
        string suggestedName,
        CancellationToken cancellationToken)
    {
        SuggestedNames.Add(suggestedName);
        return Task.FromResult(PickedPath);
    }
}
