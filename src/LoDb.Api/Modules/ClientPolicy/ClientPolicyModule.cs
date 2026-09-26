using System.Text.Json;
using System.Text.Json.Serialization;
using LoDb.Api.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.ClientPolicy;

/// <summary>
/// Client policy module: minimum and latest app versions, and the upgrade responses.
/// </summary>
/// <remarks>
/// Program.cs calls both methods from the start, so the module's owner fills this file
/// without touching a shared one. Neither method may do I/O or throw.
/// </remarks>
internal static class ClientPolicyModule
{
    public static IServiceCollection AddClientPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ClientPolicyOptions>()
            .Bind(configuration.GetSection(ClientPolicyOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<ClientPolicyOptions>, ClientPolicyOptionsValidator>());
        services.ConfigureHttpJsonOptions(static options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<ClientPlatform>(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false)));
        return services;
    }

    public static IEndpointRouteBuilder MapClientPolicy(this IEndpointRouteBuilder endpoints)
    {
        ClientPolicyEndpoint.Map(endpoints.MapGroup(ApiPaths.App));
        return endpoints;
    }
}
