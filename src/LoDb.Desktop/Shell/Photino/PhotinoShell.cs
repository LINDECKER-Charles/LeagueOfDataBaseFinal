using Photino.NET;

namespace LoDb.Desktop.Shell.Photino;

/// <summary>
/// <see cref="IDesktopShell"/> over Photino.NET (ADR 0007, confirmed by the spike): the only
/// type of the app that references Photino.
/// </summary>
internal sealed class PhotinoShell : IDesktopShell
{
    // Photino's own log prints every web message it relays, the page's included (spike
    // report, 4.4): off.
    private const int SilentLogVerbosity = 0;

    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 800;
    private const int MinimumWidth = 800;
    private const int MinimumHeight = 600;
    private const string SaveDialogTitle = "League of Data Base";
    private const string DownloadsFolder = "Downloads";
    private const string AnyNamePattern = "*";

    private volatile PhotinoWindow? _window;

    // window.external is Photino's messaging on every OS (WebView2, WKWebView, WebKitGTK).
    public string BridgeTransport =>
        "Object.freeze({send:function(m){window.external.sendMessage(m);},"
        + "listen:function(l){window.external.receiveMessage(l);}})";

    public void Run(ShellWindow window)
    {
        var photino = new PhotinoWindow()
            .SetLogVerbosity(SilentLogVerbosity)
            .SetTitle(window.Title)
            .SetUseOsDefaultSize(false)
            .SetSize(DefaultWidth, DefaultHeight)
            .SetMinSize(MinimumWidth, MinimumHeight)
            .Center()
            .SetDevToolsEnabled(window.IsDevToolsEnabled)
            .SetContextMenuEnabled(window.IsDevToolsEnabled)
            .SetTemporaryFilesPath(window.WebViewDirectory)
            .RegisterWebMessageReceivedHandler(
                (_, message) => _ = Task.Run(() => window.OnMessage(message)))
            .RegisterWindowClosingHandler((_, _) =>
            {
                window.OnClosing();

                // Never cancelled: macOS would ignore it anyway (spike report, 4.1).
                return false;
            });
        _window = photino;
        photino.Load(window.Address);
        photino.WaitForClose();
    }

    public void Close()
    {
        // Photino's calls into the native window belong to the UI thread.
        var window = _window;
        window?.Invoke(window.Close);
    }

    public void Post(string message) => _window?.SendWebMessage(message);

    /// <remarks>
    /// Photino cannot prefill the name (spike report, 4.4): the dialog opens on the
    /// Downloads folder with a filter on the suggested extension, which the OS appends.
    /// </remarks>
    public async Task<string?> PickSaveFileAsync(
        string suggestedName,
        CancellationToken cancellationToken)
    {
        var window = _window ?? throw new InvalidOperationException("No window is running.");
        var path = await window.ShowSaveFileAsync(
            SaveDialogTitle,
            DownloadsPath(),
            FiltersFor(suggestedName));
        return string.IsNullOrEmpty(path) ? null : path;
    }

    private static (string Name, string[] Extensions)[] FiltersFor(string suggestedName)
    {
        var extension = Path.GetExtension(suggestedName);
        return extension.Length > 1
            ? [(extension[1..].ToUpperInvariant(), [AnyNamePattern + extension])]
            : [];
    }

    private static string? DownloadsPath()
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            DownloadsFolder);
        return Directory.Exists(downloads) ? downloads : null;
    }
}
