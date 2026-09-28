using LoDb.Api.Hosting;
using LoDb.Api.Modules.Audit.Http;
using LoDb.Api.Modules.PublicApi.Keys.Reference;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi.Keys;

/// <summary>
/// The API key of the signed-in account, as the portal <c>/{locale}/account/api</c> manages
/// it: its overview, its creation, its regeneration and its revocation; and the reference
/// the page <c>/{locale}/developers</c> documents.
/// </summary>
/// <remarks>
/// One active key per account, as in the legacy portal. Every change is audited and reported
/// to <see cref="IApiKeyCache"/> once committed, so <c>/v1</c> applies it to the next request.
/// </remarks>
internal static class ApiKeyPortal
{
    /// <summary>The key of the signed-in account.</summary>
    public const string Path = ApiPaths.App + "/account/api-key";

    /// <summary>The generated client gets one service per tag.</summary>
    public const string Tag = "ApiKeys";

    public static IServiceCollection AddApiKeyPortal(this IServiceCollection services)
    {
        services.TryAddScoped<KeyOverviews>();
        services.TryAddScoped<OwnedKeys>();
        services.TryAddScoped<ApiKeyEndpoints>();
        services.TryAddScoped<ReferenceEndpoint>();
        services.AddOptions<ReferenceOptions>()
            .BindConfiguration(ReferenceOptions.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<ReferenceOptions>, ReferenceOptionsValidator>());
        return services;
    }

    public static IEndpointRouteBuilder MapApiKeyPortal(this IEndpointRouteBuilder endpoints)
    {
        // Answers follow the session, and two of them carry a secret.
        var keys = endpoints.MapGroup(Path)
            .WithTags(Tag)
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .AddEndpointFilter<NoStoreFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        ApiKeyEndpoints.Map(keys);
        ReferenceEndpoint.Map(endpoints);
        return endpoints;
    }
}
