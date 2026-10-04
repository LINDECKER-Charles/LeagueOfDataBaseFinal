using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoDb.Api.Hosting.Health;

/// <summary>
/// Liveness (<c>/healthz</c>) and readiness (<c>/readyz</c>) probes.
/// </summary>
/// <remarks>
/// Liveness runs no check: a database outage must not make Docker restart the API.
/// Readiness checks what serving needs, the database and a writable storage root.
/// </remarks>
internal static class HealthRegistration
{
    public const string LivenessPath = "/healthz";
    public const string ReadinessPath = "/readyz";
    private const string ReadyTag = "ready";
    private const string PostgresCheck = "postgres";
    private const string StorageCheck = "storage";
    private const int CheckTimeoutSeconds = 5;

    public static IServiceCollection AddLoDbHealthChecks(this IServiceCollection services)
    {
        var timeout = TimeSpan.FromSeconds(CheckTimeoutSeconds);

        // A singleton, so every probe reuses the check's connection pool.
        services.TryAddSingleton<PostgresReadinessCheck>();
        services.AddHealthChecks()
            .AddCheck<PostgresReadinessCheck>(PostgresCheck, null, [ReadyTag], timeout)
            .AddCheck<StorageReadinessCheck>(StorageCheck, null, [ReadyTag], timeout);
        return services;
    }

    public static IEndpointRouteBuilder MapLoDbHealthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        MapProbe(endpoints, LivenessPath, static _ => false);
        MapProbe(endpoints, ReadinessPath, static check => check.Tags.Contains(ReadyTag));
        return endpoints;
    }

    private static void MapProbe(
        IEndpointRouteBuilder endpoints,
        string path,
        Func<HealthCheckRegistration, bool> predicate)
    {
        var options = new HealthCheckOptions
        {
            Predicate = predicate,
            ResponseWriter = HealthResponseWriter.WriteAsync,
        };
        endpoints.MapHealthChecks(path, options).AllowAnonymous().DisableRateLimiting();
    }
}
