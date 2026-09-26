using System.Diagnostics.CodeAnalysis;

namespace LoDb.Api.Modules.Accounts.Http;

/// <summary>Web origins as browsers write them: scheme, host and port, nothing else.</summary>
internal static class WebOrigin
{
    private const string RootPath = "/";

    /// <summary>
    /// Reads an http(s) origin such as <c>https://example.com</c>: no path, query, fragment
    /// or user info.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out Uri? origin)
    {
        origin = null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.AbsolutePath != RootPath
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0)
        {
            return false;
        }

        origin = uri;
        return true;
    }

    /// <summary>The origin as a browser sends it, default port left out.</summary>
    public static string Format(Uri origin) => origin.GetLeftPart(UriPartial.Authority);

    /// <summary>The origin the request was addressed to, as the proxy forwarded it.</summary>
    public static string Of(HttpRequest request) => $"{request.Scheme}://{request.Host}";
}
