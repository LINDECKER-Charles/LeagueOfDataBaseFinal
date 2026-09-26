namespace LoDb.Desktop.Hosting;

/// <summary>Settings of one run of the host, fixed before it starts.</summary>
internal sealed record DesktopOptions
{
    /// <summary>The release version, sent in <c>X-LoDb-Client</c> and shown to the front.</summary>
    public required string Version { get; init; }

    public required string Channel { get; init; }

    /// <summary>Origin of the remote API that <c>/api/**</c> is relayed to.</summary>
    public required Uri ApiOrigin { get; init; }

    /// <summary>The <c>shell</c> build of the front, served on loopback.</summary>
    public required string ShellDirectory { get; init; }

    /// <summary>
    /// Per-user data (keys, refresh token, WebView profile), never under the install folder
    /// that an update replaces (ADR 0007).
    /// </summary>
    public required string DataDirectory { get; init; }

    /// <summary>The "desktop app" OAuth client of Google; null disables Google sign-in.</summary>
    public string? GoogleClientId { get; init; }

    public bool IsDevToolsEnabled { get; init; }
}
