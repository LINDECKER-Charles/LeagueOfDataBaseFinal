namespace LoDb.Api.Modules.Analytics;

/// <summary>
/// Analytics module: capture of the router beacon and the back office reports.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class AnalyticsModule
{
    public static IServiceCollection AddAnalytics(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapAnalytics(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
