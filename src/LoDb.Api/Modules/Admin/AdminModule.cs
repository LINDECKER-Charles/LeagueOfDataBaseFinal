namespace LoDb.Api.Modules.Admin;

/// <summary>
/// Admin module: the back office API, its role and its TOTP second factor.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class AdminModule
{
    public static IServiceCollection AddAdmin(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
