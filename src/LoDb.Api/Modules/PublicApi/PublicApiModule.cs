namespace LoDb.Api.Modules.PublicApi;

/// <summary>
/// Public API module: the <c>/v1</c> endpoints with keys, rate limits, quotas and credits.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class PublicApiModule
{
    public static IServiceCollection AddPublicApi(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapPublicApi(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
