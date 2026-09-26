namespace LoDb.Api.Modules.Builds;

/// <summary>
/// Builds module: build rules, editor API, sharing, votes and import.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class BuildsModule
{
    public static IServiceCollection AddBuilds(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapBuilds(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
