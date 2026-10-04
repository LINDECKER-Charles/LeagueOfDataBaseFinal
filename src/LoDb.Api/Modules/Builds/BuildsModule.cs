using LoDb.Api.Hosting;
using LoDb.Api.Modules.Builds.Catalogs;
using LoDb.Api.Modules.Builds.Editing;
using LoDb.Api.Modules.Builds.Http;
using LoDb.Api.Modules.Builds.Import;
using LoDb.Api.Modules.Builds.Mine;
using LoDb.Api.Modules.Builds.Publishing;
using LoDb.Api.Modules.Builds.Sharing;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Api.Modules.Builds.Votes;
using LoDb.Api.Modules.Profiles.Cards.Builds;
using LoDb.Api.Modules.Profiles.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Builds;

/// <summary>
/// Builds module: the builds of an account, their links, votes and imports to another patch.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class BuildsModule
{
    public static IServiceCollection AddBuilds(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddScoped<BuildAccounts>();
        services.TryAddScoped<BuildAudit>();
        services.TryAddScoped<BuildVersions>();
        services.TryAddScoped<BuildCatalogReads>();
        services.TryAddScoped<OwnedBuilds>();
        services.TryAddScoped<BuildSubmissions>();
        services.TryAddScoped<BuildScores>();
        services.TryAddScoped<BallotBox>();

        // The profiles module registered an empty source first: the card now lists the
        // public builds.
        services.Replace(ServiceDescriptor.Scoped<IPublicBuildSource, PublishedBuilds>());
        AddEndpoints(services);
        return services;
    }

    public static IEndpointRouteBuilder MapBuilds(this IEndpointRouteBuilder endpoints)
    {
        var builds = endpoints.MapGroup(BuildRoutes.Builds)
            .WithTags(BuildRoutes.Tag)
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .AddEndpointFilter<NoStoreFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        MyBuildsEndpoint.Map(builds);
        CreateBuildEndpoint.Map(builds);
        ReadBuildEndpoint.Map(builds);
        UpdateBuildEndpoint.Map(builds);
        DeleteBuildEndpoint.Map(builds);
        ImportPreviewEndpoint.Map(builds);
        VoteEndpoint.Map(builds);

        // Anyone holding a link reads it; the answer follows the cookies (the caller's vote).
        var share = endpoints.MapGroup(BuildRoutes.Share)
            .WithTags(BuildRoutes.Tag)
            .AddEndpointFilter<NoStoreFilter>();
        ShareEndpoint.Map(share);
        return endpoints;
    }

    private static void AddEndpoints(IServiceCollection services)
    {
        services.TryAddScoped<MyBuildsEndpoint>();
        services.TryAddScoped<CreateBuildEndpoint>();
        services.TryAddScoped<ReadBuildEndpoint>();
        services.TryAddScoped<UpdateBuildEndpoint>();
        services.TryAddScoped<DeleteBuildEndpoint>();
        services.TryAddScoped<ImportPreviewEndpoint>();
        services.TryAddScoped<VoteEndpoint>();
        services.TryAddScoped<ShareEndpoint>();
    }
}
