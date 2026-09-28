using LoDb.Api.Hosting;
using LoDb.Api.Modules.Profiles.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.Trends;

/// <summary>
/// Trends module: popularity of the public builds.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw. The builds module
/// registers what the ranking renders with: the scores, the accounts and the catalogs.
/// </remarks>
internal static class TrendsModule
{
    /// <summary>The ranking of the public builds.</summary>
    public const string Path = ApiPaths.App + "/trends";

    /// <summary>The generated client gets one service per tag.</summary>
    public const string Tag = "Trends";

    public static IServiceCollection AddTrends(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddScoped<TrendsRanking>();
        services.TryAddScoped<TrendsEndpoint>();
        return services;
    }

    // The caller's votes make the answer personal: it never goes to a shared cache.
    public static IEndpointRouteBuilder MapTrends(this IEndpointRouteBuilder endpoints)
    {
        var trends = endpoints.MapGroup(Path)
            .WithTags(Tag)
            .AddEndpointFilter<NoStoreFilter>();
        TrendsEndpoint.Map(trends);
        return endpoints;
    }
}
