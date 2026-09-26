using System.Net;
using LoDb.Ingestion.Egress.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Egress;

/// <summary>
/// Registrations of the egress zone: the filtered outbound HTTP client of the ingestion.
/// </summary>
/// <remarks>
/// Program.cs calls it from the start and the zone's owner fills it, so no shared file
/// changes. It must neither do I/O nor throw: the OpenAPI generation builds the host.
/// </remarks>
public static class EgressRegistration
{
    /// <summary>
    /// Name of the filtered client in <see cref="IHttpClientFactory"/>.
    /// </summary>
    public const string ClientName = "ddragon";

    public static IServiceCollection AddLoDbEgress(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(EgressOptions.SectionName);
        services.AddOptions<EgressOptions>()
            .Bind(section)
            .PostConfigure(options => ApplyDefaultHosts(options, section))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<EgressOptions>, EgressOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AllowList>();
        services.TryAddTransient<AllowListHandler>();
        services.TryAddTransient<RedirectHandler>();
        services.TryAddSingleton<IEgressFetcher, EgressFetcher>();
        AddClient(services);

        // The resilience handler names its pipeline "<client>-<handler>".
        EgressTelemetry.Quiet(services, $"{ClientName}-{EgressResilience.PipelineName}");
        return services;
    }

    // Only when the key is absent: a key set to nothing must reach the validator and fail.
    private static void ApplyDefaultHosts(EgressOptions options, IConfigurationSection section)
    {
        if (options.AllowedHosts.Count > 0
            || section.GetSection(nameof(EgressOptions.AllowedHosts)).Exists())
        {
            return;
        }

        foreach (var host in EgressOptions.DefaultAllowedHosts)
        {
            options.AllowedHosts.Add(host);
        }
    }

    // Outermost first: redirects, then the allow-list on every hop, then the resilience
    // pipeline, so a refusal is never retried and each hop gets its own retries.
    private static void AddClient(IServiceCollection services)
    {
        services.AddHttpClient(ClientName)
            .ConfigureHttpClient(ConfigureClient)
            .ConfigurePrimaryHttpMessageHandler(
                static provider => EgressPrimaryHandler.Create(OptionsOf(provider)))
            // A cold ingestion is thousands of requests: no line per request.
            .RemoveAllLoggers()
            .AddHttpMessageHandler<RedirectHandler>()
            .AddHttpMessageHandler<AllowListHandler>()
            .AddResilienceHandler(
                EgressResilience.PipelineName,
                static (builder, context) =>
                    EgressResilience.Configure(builder, OptionsOf(context.ServiceProvider)))
            // One breaker per host: an outage of CommunityDragon leaves Data Dragon alone.
            .SelectPipelineByAuthority();
    }

    private static void ConfigureClient(HttpClient client)
    {
        client.DefaultRequestVersion = HttpVersion.Version11;
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

        // Timeouts belong to the resilience pipeline and to the body deadline.
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    private static EgressOptions OptionsOf(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<EgressOptions>>().Value;
}
