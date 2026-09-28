namespace LoDb.Desktop.Hosting;

/// <summary>The local routes of the host, next to the shell files and the proxy.</summary>
internal static class DesktopRoutes
{
    /// <summary>The token endpoints of the front's <c>host</c> strategy (plan §5.2).</summary>
    public const string Auth = "/desktop/auth";

    /// <summary>Loopback redirect URI of the Google sign-in (RFC 8252).</summary>
    public const string GoogleCallback = Auth + "/google/callback";

    /// <summary>Liveness of the host itself, for the smoke check.</summary>
    public const string Health = "/desktop/health";

    /// <summary>The remote API, relayed by the proxy.</summary>
    public const string Api = "/api";

    /// <summary>Everything local that is not a shell file.</summary>
    public const string Desktop = "/desktop";
}
