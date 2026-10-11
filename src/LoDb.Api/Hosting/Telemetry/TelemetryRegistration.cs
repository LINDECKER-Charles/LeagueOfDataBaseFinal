using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LoDb.Api.Hosting.Telemetry;

/// <summary>
/// OpenTelemetry metrics, scraped by Prometheus on the metrics port, and traces.
/// </summary>
/// <remarks>
/// Traces exist even without an exporter: the W3C trace id they carry correlates the logs.
/// They are exported over OTLP only once the infrastructure sets the endpoint, a share of
/// them (<see cref="TelemetryOptions.TraceSampleRatio"/>); until then no span is recorded.
/// The metric names and their labels are a contract (docs/guides/metriques.md).
/// </remarks>
internal static class TelemetryRegistration
{
    public const string ServiceName = "lodb-api";
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    private const string RequestDurationInstrument = "http.server.request.duration";

    // Nothing reads a span until an exporter is set: without one, no trace is recorded.
    private const double UnexportedRatio = 0;

    // "LoDb.*" takes every meter and activity source the modules and workers create. The
    // ASP.NET Core instrumentation leaves out the framework's meters of sign-ins, lockouts and
    // two-factor checks (Identity), of challenges and forbids (Authentication) and of the
    // policies' results (Authorization): the security alerts read them.
    private static readonly string[] Meters =
    [
        "LoDb.*",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.AspNetCore.Identity",
        "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Authorization",
    ];

    private static readonly string[] Sources = ["LoDb.*", "Npgsql"];

    // Every bound is one more series per route, method and status: 8 instead of the
    // framework's 14 keep the scrape within its series budget, from a read the API holds in
    // memory (10 ms) to a cold catalog load (10 s, the renderer's wait on one call).
    private static readonly double[] RequestDurationBounds =
        [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 10];

    public static IServiceCollection AddLoDbTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var exportTraces = !string.IsNullOrWhiteSpace(configuration[OtlpEndpointKey]);
        services.AddOptions<TelemetryOptions>()
            .BindConfiguration(TelemetryOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
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
            .AddView(
                RequestDurationInstrument,
                new ExplicitBucketHistogramConfiguration { Boundaries = RequestDurationBounds })
            .AddPrometheusExporter()
            // Created with the provider, so the gauge exists before the first scrape.
            .AddInstrumentation(static services => services.GetRequiredService<BuildInfoMetrics>());

    private static void ConfigureTracing(TracerProviderBuilder tracing, bool exportTraces)
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(Sources)
            .SetSampler(services =>
                TraceSampling.Of(exportTraces ? RatioOf(services) : UnexportedRatio));
        if (exportTraces)
        {
            tracing.AddOtlpExporter();
        }
    }

    private static double RatioOf(IServiceProvider services) =>
        services.GetRequiredService<IOptions<TelemetryOptions>>().Value.TraceSampleRatio;
}
