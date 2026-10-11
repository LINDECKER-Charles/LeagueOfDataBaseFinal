using System.Net;
using LoDb.Infrastructure.Audience.Umami;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Audience;

/// <summary>
/// Registrations of the audience pulled from Umami: its settings, the <c>umami</c> client and
/// its allow-list, the pull, its store and its gauge.
/// </summary>
/// <remarks>
/// Called by the analytics zone, which the audience is to replace: neither I/O nor throw, with
/// an empty configuration as well (the OpenAPI generation builds the host). The API registers
/// the <see cref="Analytics.Capture.ITrackedPageResolver"/> and the hourly job.
/// </remarks>
public static class AudienceRegistration
{
    public static IServiceCollection AddLoDbAudience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AudienceOptions>()
            .Bind(configuration.GetSection(AudienceOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<AudienceOptions>, AudienceOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AudienceMetrics>();
        services.TryAddSingleton<AudienceStore>();
        services.TryAddSingleton<UmamiClient>();
        services.TryAddSingleton<IAudiencePull, AudiencePull>();
        services.TryAddTransient<UmamiHostHandler>();
        AddClient(services);
        return services;
    }

    // No redirect followed nor cookie kept: the account's token only goes to the one host.
    // An hourly pull logs one summary line, never one per request.
    private static void AddClient(IServiceCollection services) =>
        services.AddHttpClient(UmamiClient.ClientName)
            .ConfigureHttpClient(static (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<AudienceOptions>>().Value;
                client.Timeout = options.RequestTimeout;
                client.MaxResponseContentBufferSize = options.MaxResponseBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All,
            })
            .RemoveAllLoggers()
            .AddHttpMessageHandler<UmamiHostHandler>();
}
