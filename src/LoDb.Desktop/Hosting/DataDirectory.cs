namespace LoDb.Desktop.Hosting;

/// <summary>Layout of the per-user data folder of the app.</summary>
internal static class DataDirectory
{
    /// <summary>Data Protection keys, which encrypt the refresh token at rest.</summary>
    public const string KeysFolder = "keys";

    /// <summary>The encrypted refresh token of a "remember me" sign-in.</summary>
    public const string SessionFolder = "session";

    /// <summary>The WebView profile (WebView2 user data folder).</summary>
    public const string WebViewFolder = "webview";

    // Distinct from the Velopack pack id ("LoDb.Desktop"), whose folder holds the install.
    private const string AppFolder = "LeagueOfDataBase";

    private const UnixFileMode OwnerOnly =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    public static string DefaultFor(string channel) => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify),
        AppFolder,
        channel);

    /// <summary>Creates the data folder and its subfolders, readable by the user only.</summary>
    public static void Prepare(string root)
    {
        CreatePrivate(root);
        CreatePrivate(Path.Combine(root, KeysFolder));
        CreatePrivate(Path.Combine(root, SessionFolder));
        CreatePrivate(Path.Combine(root, WebViewFolder));
    }

    /// <summary>
    /// Creates a folder as owner-only on Unix; Windows already restricts the user's local
    /// application data to its owner.
    /// </summary>
    public static void CreatePrivate(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, OwnerOnly);
    }
}
