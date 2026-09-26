using LoDb.Api.Hosting;
using LoDb.Api.Modules.Profiles.Cards;
using LoDb.Api.Modules.Profiles.Cards.Builds;
using LoDb.Api.Modules.Profiles.Curation;
using LoDb.Api.Modules.Profiles.Deletion;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Api.Modules.Profiles.Identity;
using LoDb.Api.Modules.Profiles.Owner;
using LoDb.Api.Modules.Profiles.Reading;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        IConfiguration configuration)
    {
        services.AddProfileJson();
        services.TryAddScoped<ProfileOwners>();
        services.TryAddScoped<ProfileAudit>();
        services.TryAddScoped<ProfileCatalog>();
        services.TryAddScoped<PublicCards>();

        // The builds module replaces it with its own source (IPublicBuildSource).
        services.TryAddScoped<IPublicBuildSource, NoPublicBuilds>();
        AddEndpoints(services);
        return services;
    }

    public static IEndpointRouteBuilder MapProfiles(this IEndpointRouteBuilder endpoints)
    {
        var own = endpoints.MapGroup(ProfileRoutes.Own)
            .WithTags(ProfileRoutes.Tag)
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .AddEndpointFilter<NoStoreFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        OwnerProfileEndpoint.Map(own);
        PreviewEndpoint.Map(own);
        SaveFavoritesEndpoint.Map(own);
        VisibilityEndpoint.Map(own);
        PreferredVersionEndpoint.Map(own);
        IdentityEndpoint.Map(own);
        SetPasswordEndpoint.Map(own);
        DeleteAccountEndpoint.Map(own);

        var profiles = endpoints.MapGroup(ProfileRoutes.Public)
            .WithTags(ProfileRoutes.Tag)
            .AddEndpointFilter<NoStoreFilter>();
        PublicProfileEndpoint.Map(profiles);
        return endpoints;
    }

    private static void AddEndpoints(IServiceCollection services)
    {
        services.TryAddScoped<OwnerProfileEndpoint>();
        services.TryAddScoped<PreviewEndpoint>();
        services.TryAddScoped<SaveFavoritesEndpoint>();
        services.TryAddScoped<VisibilityEndpoint>();
        services.TryAddScoped<PreferredVersionEndpoint>();
        services.TryAddScoped<IdentityEndpoint>();
        services.TryAddScoped<SetPasswordEndpoint>();
        services.TryAddScoped<DeleteAccountEndpoint>();
        services.TryAddScoped<PublicProfileEndpoint>();
    }
}
