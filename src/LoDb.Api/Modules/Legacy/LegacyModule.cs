namespace LoDb.Api.Modules.Legacy;

/// <summary>
/// Legacy module: resolution of the former site's URLs into permanent redirects.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class LegacyModule
{
    public static IServiceCollection AddLegacy(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapLegacy(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
