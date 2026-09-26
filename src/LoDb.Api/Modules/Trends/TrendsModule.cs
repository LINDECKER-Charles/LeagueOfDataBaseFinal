namespace LoDb.Api.Modules.Trends;

/// <summary>
/// Trends module: popularity of the public builds.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class TrendsModule
{
    public static IServiceCollection AddTrends(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapTrends(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
