using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Hosting;

/// <summary>
/// Answers 404, and nothing else, to a connection on the API port that comes neither from the
/// stack's own networks nor from the loopback (<see cref="StackNetwork"/>).
/// </summary>
/// <remarks>
/// <para>
/// On a served host the API also sits on a network shared with other projects
/// (<c>observability</c>, for the scrape): without the gate, any of their containers could call
/// the anonymous endpoints the renderer and nginx use, and write the forwarded headers of a
/// trusted proxy. Placed before the forwarded headers are applied, so the gate judges the
/// connection itself. The metrics port is answered before it, and the healthcheck command calls
/// the loopback.
/// </para>
/// <para>
/// A refusal is counted (<see cref="NetworkGateMetrics"/>), never logged: one line per
/// request would let a probe flood the logs.
/// </para>
/// </remarks>
internal static class NetworkGate
{
    public static IServiceCollection AddLoDbNetworkGate(this IServiceCollection services)
    {
        services.TryAddSingleton<StackNetwork>();
        services.TryAddSingleton<NetworkGateMetrics>();
        return services;
    }

    public static IApplicationBuilder UseLoDbNetworkGate(this IApplicationBuilder app) =>
        app.Use(static (context, next) =>
        {
            // Resolved per request rather than here: an invalid network must fail the host
            // start with its validation message, not the pipeline construction.
            var services = context.RequestServices;
            if (services.GetRequiredService<StackNetwork>().Contains(
                context.Connection.RemoteIpAddress))
            {
                return next(context);
            }

            services.GetRequiredService<NetworkGateMetrics>().RecordRefusal();
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });
}
