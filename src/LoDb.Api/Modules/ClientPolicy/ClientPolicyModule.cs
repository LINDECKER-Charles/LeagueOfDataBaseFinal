using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Api.Hosting;
using LoDb.Api.Modules.ClientPolicy.Gate;
using LoDb.Api.Modules.ClientPolicy.Policy;
using LoDb.Api.Modules.ClientPolicy.Publishing;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoDb.Api.Modules.ClientPolicy;

/// <summary>
/// Client policy module: minimum and latest app versions, Android's live bundle, and the
/// 426 answered to an app below its minimum (ADR 0008).
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw. The policy is
/// published by an administrator here, or by <c>client-policy publish</c> in
/// <c>Cli/ClientPolicy</c>.
/// </remarks>
internal static class ClientPolicyModule
{
    public static IServiceCollection AddClientPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHybridCache();
        services.AddMetrics();
        services.TryAddSingleton<ClientPolicyStore>();
        services.TryAddSingleton<ClientPolicyMetrics>();
        services.TryAddScoped<PublishPolicyEndpoint>();
        services.ConfigureHttpJsonOptions(static options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<ClientPlatform>(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false)));
        return services;
    }

    public static IEndpointRouteBuilder MapClientPolicy(this IEndpointRouteBuilder endpoints)
    {
        UseVersionGate(endpoints);
        ClientPolicyEndpoint.Map(endpoints);
        var admin = endpoints.MapGroup(ClientPolicyRoutes.Admin)
            .WithTags(ClientPolicyRoutes.AdminTag)
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        PublishPolicyEndpoint.Map(admin);
        return endpoints;
    }

    // Program.cs maps the modules once the host's middleware is in place, and never changes:
    // added here, the gate runs after routing, CORS and authentication, just before the
    // endpoints. After CORS matters: the Android app, a cross-origin caller, can only read a
    // 426 that carries the CORS headers.
    private static void UseVersionGate(IEndpointRouteBuilder endpoints)
    {
        if (endpoints is IApplicationBuilder app)
        {
            app.UseMiddleware<ClientVersionGate>();
        }
    }
}
