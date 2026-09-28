using LoDb.Desktop.Hosting;
using Yarp.ReverseProxy.Forwarder;

namespace LoDb.Desktop.Proxy;

/// <summary>
/// <c>/api/**</c> → the remote API (YARP direct forwarding): the WebView keeps a single
/// origin, so no CORS on a random port, and never holds a token (ADR 0007).
/// </summary>
internal static class ApiProxy
{
    /// <summary>The route of the proxy, which the smoke check looks for.</summary>
    public const string Pattern = DesktopRoutes.Api + "/{**path}";

    public static IServiceCollection AddApiProxy(this IServiceCollection services)
    {
        services.AddHttpForwarder();
        services.AddSingleton<ApiRequestTransformer>();
        return services;
    }

    public static void MapApiProxy(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<DesktopOptions>();
        endpoints.MapForwarder(
            Pattern,
            options.ApiOrigin.GetLeftPart(UriPartial.Authority),
            ForwarderRequestConfig.Empty,
            endpoints.ServiceProvider.GetRequiredService<ApiRequestTransformer>());
    }
}
