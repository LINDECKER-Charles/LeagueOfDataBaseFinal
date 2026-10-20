using System.Net;
using LoDb.Infrastructure.PageStore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.PageStore;

/// <summary>
/// Registrations of the page store: its settings, the <c>page-store</c> client with its
/// allow-list and its signature, and <see cref="IPageStore"/>.
/// </summary>
/// <remarks>
/// Called by the pre-render module. Neither I/O nor throw, with an empty configuration as well
/// (the OpenAPI generation builds the host): an unset store stays dormant.
/// </remarks>
public static class PageStoreRegistration
{
    /// <summary>Name of the store's client in <see cref="IHttpClientFactory"/>.</summary>
    public const string ClientName = "page-store";

    // Answers are read whole: an S3 answer is a few hundred bytes, anything far larger is not
    // the store speaking.
    private const int MaxAnswerBytes = 64 * 1024;

    public static IServiceCollection AddLoDbPageStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PageStoreOptions>()
            .Bind(configuration.GetSection(PageStoreOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<PageStoreOptions>, PageStoreOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPageStore, S3PageStore>();
        services.TryAddTransient<PageStoreHostHandler>();
        services.TryAddTransient<PageStoreSigningHandler>();
        AddClient(services);
        return services;
    }

    // No redirect followed (a signed request goes to the endpoint alone), no cookie, and the
    // bodies left as they are: a page is stored gzip and must stay so. No log line per
    // request: a pass writes millions of objects. The allow-list runs before the signature, so
    // a refused destination is never signed.
    private static void AddClient(IServiceCollection services) =>
        services.AddHttpClient(ClientName)
            .ConfigureHttpClient(static (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<PageStoreOptions>>().Value;
                client.Timeout = options.RequestTimeout;
                client.MaxResponseContentBufferSize = MaxAnswerBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.None,
            })
            .RemoveAllLoggers()
            .AddHttpMessageHandler<PageStoreHostHandler>()
            .AddHttpMessageHandler<PageStoreSigningHandler>();
}
