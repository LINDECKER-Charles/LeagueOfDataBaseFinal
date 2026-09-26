using LoDb.Api.Hosting;
using LoDb.Api.Modules.Analytics.Admin;
using LoDb.Api.Modules.Analytics.Capture;
using LoDb.Api.Modules.Analytics.Pages;
using LoDb.Api.Modules.Audit.Http;
using LoDb.Infrastructure.Analytics.Capture;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Analytics;

/// <summary>
/// Analytics module: capture of the router beacon and the back office reports.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw. The views are taken in
/// by nginx's mirror and the router's beacon, resolved and written by
/// <c>Workers/Analytics</c>, which also folds and keeps them; the legacy aggregates are
/// imported by <c>Cli/Analytics</c>.
/// </remarks>
internal static class AnalyticsModule
{
    public static IServiceCollection AddAnalytics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ITrackedPageResolver, CatalogPageResolver>();
        services.TryAddSingleton<ViewIntake>();
        services.TryAddScoped<RollupEndpoint>();
        services.Configure<RateLimiterOptions>(BeaconRateLimit.AddTo);
        return services;
    }

    public static IEndpointRouteBuilder MapAnalytics(this IEndpointRouteBuilder endpoints)
    {
        MirrorEndpoint.Map(endpoints);
        BeaconEndpoint.Map(endpoints);

        // Reports hold no address, but follow the session: never cached either.
        var admin = endpoints.MapGroup(AnalyticsAdminRoutes.Prefix)
            .WithTags(AnalyticsAdminRoutes.Tag)
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .AddEndpointFilter<NoStoreFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        ReportEndpoint.Map(admin);
        RollupEndpoint.Map(admin);
        return endpoints;
    }
}
