using System.Net;

namespace LoDb.Desktop.Hosting;

/// <summary>
/// Keeps the loopback host to the app's own page. Any page of the user's browser can send
/// requests to 127.0.0.1: without this guard, a site that guessed the port could sign in,
/// sign out or call the API with the user's token through the proxy.
/// </summary>
internal sealed partial class LoopbackGuard(RequestDelegate next, ILogger<LoopbackGuard> logger)
{
    private const string FetchSiteHeader = "Sec-Fetch-Site";
    private const string SameOrigin = "same-origin";

    // A navigation typed or loaded by the host itself, such as the WebView's first load.
    private const string UserInitiated = "none";

    private const string HostReason = "host";
    private const string OriginReason = "origin";
    private const string SiteReason = "site";

    public Task InvokeAsync(HttpContext context)
    {
        var reason = RefusalOf(context);
        if (reason is null)
        {
            return next(context);
        }

        LogRefused(logger, reason);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static string? RefusalOf(HttpContext context)
    {
        var request = context.Request;
        var ownHost = $"{IPAddress.Loopback}:{context.Connection.LocalPort}";

        // A DNS name that resolves to 127.0.0.1 (DNS rebinding) is not this origin.
        if (!string.Equals(request.Host.Value, ownHost, StringComparison.Ordinal))
        {
            return HostReason;
        }

        // Google's redirect is a cross-site navigation by design; its state guards it.
        if (request.Path.Equals(DesktopRoutes.GoogleCallback, StringComparison.Ordinal))
        {
            return null;
        }

        var origin = request.Headers.Origin.ToString();
        if (origin.Length > 0 && origin != $"{Uri.UriSchemeHttp}://{ownHost}")
        {
            return OriginReason;
        }

        var site = request.Headers[FetchSiteHeader].ToString();
        return site.Length > 0 && site is not (SameOrigin or UserInitiated) ? SiteReason : null;
    }

    [LoggerMessage(
        EventName = "desktop.loopback.refused",
        Level = LogLevel.Warning,
        Message = "A loopback request was refused on its {Reason}.")]
    private static partial void LogRefused(ILogger logger, string reason);
}
