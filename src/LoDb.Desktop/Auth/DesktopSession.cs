using LoDb.Desktop.Auth.Google;

namespace LoDb.Desktop.Auth;

/// <summary>
/// Answer of <c>GET /desktop/auth/session</c>: what the host holds, never a token. The
/// user's identity is read from <c>/api/account/me</c>, through the proxy, as on the web.
/// </summary>
internal sealed record DesktopSession
{
    /// <summary>The host holds a refresh token (it may still be refused on next use).</summary>
    public required bool SignedIn { get; init; }

    /// <summary>The refresh token is kept, encrypted, across restarts.</summary>
    public required bool Remembered { get; init; }

    /// <summary>The last Google sign-in, which the front polls after opening it.</summary>
    public required GoogleStage Google { get; init; }

    public string? GoogleFailure { get; init; }
}
