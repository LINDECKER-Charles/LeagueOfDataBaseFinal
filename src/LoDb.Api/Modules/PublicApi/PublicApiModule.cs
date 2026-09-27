using LoDb.Api.Hosting;
using LoDb.Api.Hosting.OpenApi;
using LoDb.Api.Modules.PublicApi.Access;
using LoDb.Api.Modules.PublicApi.Builds;
using LoDb.Api.Modules.PublicApi.Gate;
using LoDb.Api.Modules.PublicApi.Keys;
using LoDb.Api.Modules.PublicApi.Limits;
using LoDb.Api.Modules.PublicApi.Metering;
using LoDb.Api.Modules.PublicApi.OpenApi;
using LoDb.Api.Modules.PublicApi.Profiles;
using LoDb.Api.Modules.PublicApi.Trends;
using LoDb.Api.Modules.PublicApi.Trends.Reading;
using LoDb.Api.Modules.PublicApi.Usage;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi;

/// <summary>
/// Public API module: the <c>/v1</c> endpoints with keys, rate limits, quotas and credits.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw. <c>/v1</c> keeps
/// go-api's contract (<c>docs/reecriture/rapports/contrat-v1.md</c>): its envelope, its
/// names, its plain-text 404 and 405, and its order: the key, the rate limit, the quota,
/// then the handler.
/// </remarks>
internal static class PublicApiModule
{
    /// <summary>The tag of every operation of the <c>public-v1</c> document.</summary>
    public const string Tag = "Public API";

    public static IServiceCollection AddPublicApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PublicApiOptions>()
            .Bind(configuration.GetSection(PublicApiOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<PublicApiOptions>, PublicApiOptionsValidator>());
        services.AddHybridCache();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<UsageCalendar>();
        services.TryAddSingleton<PublicApiMetrics>();
        AddAccess(services);
        AddTrends(services);
        services.TryAddScoped<ProfileEndpoint>();
        services.TryAddScoped<ChampionBuildsEndpoint>();
        services.TryAddScoped<UsageEndpoint>();
        services.AddApiKeyPortal();
        services.Configure<OpenApiOptions>(
            OpenApiDocuments.PublicV1,
            static options => options.AddDocumentTransformer<PublicApiDocumentTransformer>());
        return services;
    }

    public static IEndpointRouteBuilder MapPublicApi(this IEndpointRouteBuilder endpoints)
    {
        var v1 = endpoints.MapGroup(ApiPaths.PublicV1).WithTags(Tag);

        var billed = v1.MapGroup(string.Empty).AddEndpointFilter<PublicApiGate>();
        ProfileEndpoint.Map(billed);
        ChampionBuildsEndpoint.Map(billed);
        TrendsEndpoint.Map(billed);

        var free = v1.MapGroup(string.Empty)
            .AddEndpointFilter<PublicApiGate>()
            .WithMetadata(QuotaExemption.Instance);
        UsageEndpoint.Map(free);

        v1.MapRouterAnswers(
        [
            ProfileEndpoint.Pattern,
            ChampionBuildsEndpoint.Pattern,
            TrendsEndpoint.Pattern,
            UsageEndpoint.Pattern,
        ]);
        endpoints.MapApiKeyPortal();
        return endpoints;
    }

    // Singletons: the gate is built once, with the application's services.
    private static void AddAccess(IServiceCollection services)
    {
        services.TryAddSingleton<ApiKeyStore>();
        services.TryAddSingleton<UnknownKeys>();
        services.TryAddSingleton<ApiKeyDirectory>();
        services.TryAddSingleton<IApiKeyCache>(
            static provider => provider.GetRequiredService<ApiKeyDirectory>());
        services.TryAddSingleton<ApiRateLimiter>();
        services.TryAddSingleton<ApiQuota>();
        services.TryAddSingleton<UsageWriter>();
        services.TryAddSingleton<UsageMeter>();
        services.TryAddSingleton<ApiAccess>();

        // Wherever /v1 is served, workers switched off included; never while the document
        // is generated at build time.
        if (!OpenApiDocuments.IsBuildTimeGeneration)
        {
            services.AddHostedService<UsageFlusher>();
        }
    }

    private static void AddTrends(IServiceCollection services)
    {
        services.TryAddSingleton<TrendsSource>();
        services.TryAddSingleton<ITrendNames, CatalogTrendNames>();
        services.TryAddSingleton<TrendsService>();
        services.TryAddScoped<TrendsEndpoint>();
    }
}
