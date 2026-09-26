namespace LoDb.Api.Modules.Profiles;

/// <summary>
/// Profiles module: own and public profiles, favourites, deletion and bans.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class ProfilesModule
{
    public static IServiceCollection AddProfiles(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static IEndpointRouteBuilder MapProfiles(this IEndpointRouteBuilder endpoints) =>
        endpoints;
}
