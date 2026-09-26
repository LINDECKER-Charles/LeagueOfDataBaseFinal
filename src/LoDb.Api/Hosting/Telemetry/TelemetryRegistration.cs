using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LoDb.Api.Hosting.Telemetry;

/// <summary>
/// OpenTelemetry metrics, scraped by Prometheus on the metrics port, and traces.
/// </summary>
/// <remarks>
/// Traces exist even without an exporter: the W3C trace id they carry correlates the logs.
/// They are exported over OTLP only once the infrastructure sets the endpoint.
/// </remarks>
internal static class TelemetryRegistration
{
    public const string ServiceName = "lodb-api";
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    // "LoDb.*" takes every meter and activity source the modules and workers create.
    private static readonly string[] Meters = ["LoDb.*", "Microsoft.EntityFrameworkCore", "Npgsql"];
    private static readonly string[] Sources = ["LoDb.*", "Npgsql"];

    public static IServiceCollection AddLoDbTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var exportTraces = !string.IsNullOrWhiteSpace(configuration[OtlpEndpointKey]);
        services.TryAddSingleton<BuildInfoMetrics>();
        services.AddOpenTelemetry()
            .ConfigureResource(static resource =>
                resource.AddService(ServiceName, serviceVersion: BuildVersion.Current))
            .WithMetrics(ConfigureMetrics)
            .WithTracing(tracing => ConfigureTracing(tracing, exportTraces));
        return services;
    }

    private static void ConfigureMetrics(MeterProviderBuilder metrics) =>
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(Meters)
            .AddPrometheusExporter()
            // Created with the provider, so the gauge exists before the first scrape.
            .AddInstrumentation(static services => services.GetRequiredService<BuildInfoMetrics>());

    private static void ConfigureTracing(TracerProviderBuilder tracing, bool exportTraces)
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(Sources);
        if (exportTraces)
        {
            tracing.AddOtlpExporter();
        }
    }
}
