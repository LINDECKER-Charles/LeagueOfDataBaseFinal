using LoDb.Api.Hosting;
using LoDb.Api.Modules.Catalog.Changes;
using LoDb.Api.Modules.Catalog.Champions;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Items;
using LoDb.Api.Modules.Catalog.Meta;
using LoDb.Api.Modules.Catalog.Pickers;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Catalog.Runes;
using LoDb.Api.Modules.Catalog.Search;
using LoDb.Api.Modules.Catalog.Summoners;
using LoDb.Api.Modules.Catalog.WarmUp;
using LoDb.Api.Modules.Catalog.WarmUp.Limits;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Catalog;

/// <summary>
/// Catalog module: metadata, lists, details, search and pickers of the Data Dragon data.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class CatalogModule
{
    private const string CatalogPrefix = "/catalog/{version}/{lang}";
    private const string PickersPrefix = "/pickers";

    public static IServiceCollection AddCatalog(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCatalogJson();
        services.TryAddSingleton<CatalogGateway>();
        services.TryAddSingleton<CanonicalGateway>();
        services.TryAddSingleton<MetaEndpoint>();
        services.TryAddSingleton(TimeProvider.System);
        AddWarmUp(services, configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapCatalog(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup(ApiPaths.App);
        MetaEndpoint.Map(api);

        var catalog = WithProblems(api.MapGroup(CatalogPrefix).WithTags(CatalogTags.Catalog));
        ChampionEndpoints.Map(catalog);
        ItemEndpoints.Map(catalog);
        RuneEndpoints.Map(catalog);
        SummonerEndpoints.Map(catalog);
        SearchEndpoint.Map(catalog);
        ChangesEndpoint.Map(catalog);
        WarmUpEndpoint.Map(catalog);

        PickerEndpoints.Map(
            WithProblems(api.MapGroup(PickersPrefix).WithTags(CatalogTags.Pickers)));
        return endpoints;
    }

    // Declared on the groups: the typed unions only tell OpenAPI about CatalogProblem, which
    // carries no status of its own.
    private static RouteGroupBuilder WithProblems(RouteGroupBuilder group) =>
        group
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    // The loader and its bounds: streams open at once, and starts per client and hour.
    private static void AddWarmUp(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WarmUpOptions>()
            .Bind(configuration.GetSection(WarmUpOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<WarmUpOptions>, WarmUpOptionsValidator>());
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<WarmUpOptions>>(static (limiter, warmUp) =>
                ColdWorkRateLimit.AddTo(limiter, warmUp.Value));
        services.TryAddSingleton<WarmUpStreams>();
        services.TryAddSingleton<CatalogWarmUp>();
    }
}
