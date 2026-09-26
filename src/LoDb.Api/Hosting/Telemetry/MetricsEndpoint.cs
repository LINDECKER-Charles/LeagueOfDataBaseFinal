using Microsoft.Extensions.Options;

namespace LoDb.Api.Hosting.Telemetry;

/// <summary>
/// The Prometheus scrape, answered on the metrics port only.
/// </summary>
/// <remarks>
/// Kestrel serves both ports with one pipeline. This branch keys on the local port: nginx,
/// which only reaches the API port, can never expose the metrics, and the metrics port
/// answers nothing but the scrape.
/// </remarks>
internal static class MetricsEndpoint
{
    public const string Path = "/metrics";

    public static IApplicationBuilder UseLoDbMetricsEndpoint(this IApplicationBuilder app)
    {
        // Read per request rather than here: an invalid port must fail the host start with
        // its validation message, not the pipeline construction the OpenAPI generation runs.
        var hosting = app.ApplicationServices.GetRequiredService<IOptions<HostingOptions>>();
        return app.MapWhen(
            context => context.Connection.LocalPort == hosting.Value.MetricsPort,
            static metrics => metrics
                .UseOpenTelemetryPrometheusScrapingEndpoint(Path)
                .Run(static context =>
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
                }));
    }
}
