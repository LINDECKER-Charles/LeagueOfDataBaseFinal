using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Analytics;

/// <summary>
/// Registrations of the analytics zone: capture storage and aggregation.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class AnalyticsRegistration
{
    public static IServiceCollection AddLoDbAnalytics(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
