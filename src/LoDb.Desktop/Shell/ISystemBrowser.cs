namespace LoDb.Desktop.Shell;

/// <summary>The user's default browser, outside the app.</summary>
internal interface ISystemBrowser
{
    /// <returns>False when no browser could be started.</returns>
    bool Open(Uri address);
}
