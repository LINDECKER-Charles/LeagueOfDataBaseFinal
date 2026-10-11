using LoDb.Api.Hosting.SecurityReports.Summary;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Hosting.SecurityReports;

/// <summary>
/// The browsers' Content-Security-Policy reports: their endpoint, its bounds, and the minute
/// summary (<c>Workers/Security</c>).
/// </summary>
internal static class SecurityReportsRegistration
{
    public static IServiceCollection AddLoDbSecurityReports(this IServiceCollection services)
    {
        services.AddOptions<SecurityReportsOptions>()
            .BindConfiguration(SecurityReportsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton<CspReportTally>();
        services.TryAddSingleton<CspReportMetrics>();
        services.TryAddSingleton<CspReportIntake>();
        services.Configure<RateLimiterOptions>(CspReportRateLimit.AddTo);
        return services;
    }

    public static IEndpointRouteBuilder MapLoDbSecurityReports(this IEndpointRouteBuilder endpoints)
    {
        CspReportEndpoint.Map(endpoints);
        return endpoints;
    }
}
