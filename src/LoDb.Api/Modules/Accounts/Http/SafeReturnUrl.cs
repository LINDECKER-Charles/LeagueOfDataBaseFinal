namespace LoDb.Api.Modules.Accounts.Http;

/// <summary>
/// The page a sign-in returns to, kept on the site: a local path, or a URL of the origin
/// the request came to, reduced to its path.
/// </summary>
/// <remarks>
/// Anything else is dropped rather than followed: a sign-in that redirects anywhere lends
/// the site's name to a phishing page.
/// </remarks>
internal static class SafeReturnUrl
{
    private const char Slash = '/';
    private const char Backslash = '\\';

    /// <summary>The path to return to, or null when <paramref name="returnUrl"/> leaves.</summary>
    public static string? Resolve(string? returnUrl, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        if (returnUrl[0] == Slash)
        {
            return IsLocalPath(returnUrl) ? returnUrl : null;
        }

        return Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute)
            && string.Equals(
                WebOrigin.Format(absolute),
                WebOrigin.Of(request),
                StringComparison.OrdinalIgnoreCase)
            ? absolute.PathAndQuery
            : null;
    }

    // "/page", but neither "//host" nor "/\host", which browsers read as another host, as
    // ASP.NET Core's local URL check.
    private static bool IsLocalPath(string path) =>
        (path.Length == 1 || (path[1] != Slash && path[1] != Backslash))
        && !path.Any(char.IsControl);
}
