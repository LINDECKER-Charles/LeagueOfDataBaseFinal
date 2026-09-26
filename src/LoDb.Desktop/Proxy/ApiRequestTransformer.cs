using System.Net.Http.Headers;
using LoDb.Desktop.Auth.Tokens;
using LoDb.Desktop.Hosting;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Forwarder;

namespace LoDb.Desktop.Proxy;

/// <summary>
/// Turns a request of the WebView into a request of the app: the host's bearer token and
/// client header in, the page's own credentials and origin out.
/// </summary>
internal sealed class ApiRequestTransformer(TokenSession session, DesktopOptions options)
    : HttpTransformer
{
    private const string BearerScheme = "Bearer";

    /// <summary>
    /// Headers the page may send but the API must not see. Cookie and Authorization: the
    /// host alone authenticates. Origin and Referer: the loopback origin is not trusted by
    /// the API's forgery rule, while a request without Origin is. X-LoDb-Client: the host
    /// states its own version.
    /// </summary>
    public static readonly string[] StrippedRequestHeaders =
    [
        HeaderNames.Cookie,
        HeaderNames.Authorization,
        HeaderNames.Origin,
        HeaderNames.Referer,
        DesktopClient.HeaderName,
    ];

    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        string destinationPrefix,
        CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(
            httpContext,
            proxyRequest,
            destinationPrefix,
            cancellationToken);
        foreach (var header in StrippedRequestHeaders)
        {
            proxyRequest.Headers.Remove(header);
        }

        proxyRequest.Headers.TryAddWithoutValidation(
            DesktopClient.HeaderName,
            DesktopClient.ValueOf(options.Version));
        var accessToken = await session.GetAccessTokenAsync(cancellationToken);
        if (accessToken is not null)
        {
            proxyRequest.Headers.Authorization =
                new AuthenticationHeaderValue(BearerScheme, accessToken);
        }
    }

    // The app is signed in by its bearer token only: a cookie of the site (the antiforgery
    // one, say) would outlive the host's session in the WebView.
    public override async ValueTask<bool> TransformResponseAsync(
        HttpContext httpContext,
        HttpResponseMessage? proxyResponse,
        CancellationToken cancellationToken)
    {
        var shouldForwardBody = await base.TransformResponseAsync(
            httpContext,
            proxyResponse,
            cancellationToken);
        httpContext.Response.Headers.Remove(HeaderNames.SetCookie);
        return shouldForwardBody;
    }
}
