using System.Diagnostics.CodeAnalysis;
using LoDb.Api.Hosting;

namespace LoDb.Api.Modules.Analytics.Capture;

/// <summary>Paths, header and rate limit of the two ways a page view comes in.</summary>
internal static class CaptureRoutes
{
    /// <summary>
    /// Target of nginx's mirror of the served pages. Outside of <c>/api</c>, which the proxy
    /// routes to the API, so that no client reaches it: nginx calls it from within.
    /// </summary>
    public const string MirrorPath = "/internal/analytics/page";

    /// <summary>Header in which the mirror passes the page's path and query.</summary>
    public const string OriginalUriHeader = "X-Original-URI";

    /// <summary>Target of the router's beacon, for the navigations inside the app.</summary>
    public const string BeaconPath = ApiPaths.App + "/analytics/view";

    /// <summary>Rate limiting policy of the beacon, by client address.</summary>
    public const string BeaconPolicy = "analytics-view";

    // Longer than any page's address: nginx itself refuses request lines beyond 8 KiB.
    private const int MaxTargetLength = 2048;

    private const char PathStart = '/';
    private const char Backslash = '\\';

    /// <summary>
    /// Whether a target reads as a path of this site: rooted, not a network path, bounded.
    /// Its grammar is checked later, off the request.
    /// </summary>
    public static bool IsTarget([NotNullWhen(true)] string? target) =>
        target is { Length: > 0 and <= MaxTargetLength }
        && target[0] == PathStart
        && (target.Length == 1 || target[1] is not (PathStart or Backslash))
        && !target.Any(char.IsControl);
}
