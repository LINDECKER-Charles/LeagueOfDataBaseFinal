using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Scaleway;

/// <summary>
/// Registrations of the Scaleway bill: its settings, the <c>scaleway-billing</c> client and
/// its reader. Neither I/O nor throw, with an empty configuration as well: unset, the bill is
/// off.
/// </summary>
internal static class ScalewayBillingRegistration
{
    // An answer of a hundred lines is some tens of kilobytes.
    private const int MaxAnswerBytes = 1024 * 1024;

    private static readonly Uri ApiBase = new("https://api.scaleway.com");

    // Per request; a reading of its six months at once has its own, longer deadline
    // (ScalewayBillReader.Deadline).
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddAdminScalewayBilling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ScalewayBillingOptions>()
            .Bind(configuration.GetSection(ScalewayBillingOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<
                IValidateOptions<ScalewayBillingOptions>,
                ScalewayBillingOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ScalewayBillingClient>();
        services.TryAddSingleton<ScalewayBillReader>();
        AddClient(services);
        return services;
    }

    // Toward Scaleway's API alone, over https, by relative paths from a fixed base: no
    // redirect followed (the key would go along), no cookie, and no log line per request,
    // whose headers hold the key.
    private static void AddClient(IServiceCollection services) =>
        services.AddHttpClient(ScalewayBillingClient.ClientName)
            .ConfigureHttpClient(static client =>
            {
                client.BaseAddress = ApiBase;
                client.Timeout = RequestTimeout;
                client.MaxResponseContentBufferSize = MaxAnswerBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            })
            .RemoveAllLoggers();
}
