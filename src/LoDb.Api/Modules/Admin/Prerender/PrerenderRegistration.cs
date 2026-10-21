using System.Net;
using LoDb.Api.Modules.Admin.Prerender.Builds;
using LoDb.Api.Modules.Admin.Prerender.Coverage;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Api.Modules.Admin.Prerender.Passes;
using LoDb.Api.Modules.Admin.Prerender.Passes.Planning;
using LoDb.Api.Modules.Admin.Prerender.Promotion;
using LoDb.Api.Modules.Admin.Prerender.Rendering;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Units;
using LoDb.Api.Modules.Admin.Prerender.Writing;
using LoDb.Infrastructure.PageStore;
using LoDb.Ingestion.Pipeline.Versions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender;

/// <summary>
/// Registrations of the pre-rendering of the catalogue (ADR 0011): its settings, the page
/// store, the <c>prerender-ssr</c> client, the pass's services and its share of each
/// promotion.
/// </summary>
/// <remarks>
/// Neither I/O nor throw, with an empty configuration as well (the OpenAPI generation builds
/// the host): unset, the pass stays dormant. The worker and the reconciliation under
/// <c>Workers/Prerender</c> resolve these services per tick and sleep while they are absent.
/// </remarks>
internal static class PrerenderRegistration
{
    // A page is some tens of kilobytes and a bundle some hundreds: far below, a bound on an
    // answer that is not one of them.
    private const int MaxAnswerBytes = 16 * 1024 * 1024;

    // Over the SSR's deadline on a render's calls to the API (LODB_RENDER_DEADLINE_SECONDS, 20 s
    // on a host, 30 in development): a slow render fails on its side first.
    private static readonly TimeSpan SsrTimeout = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddAdminPrerender(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddLoDbPageStore(configuration);
        services.AddOptions<PrerenderOptions>()
            .Bind(configuration.GetSection(PrerenderOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<PrerenderOptions>, PrerenderOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<PrerenderActivation>();
        services.TryAddSingleton<PrerenderMetrics>();
        services.TryAddSingleton<IPrerenderCatalog, CatalogPages>();
        AddPasses(services);
        AddEngine(services);
        AddPromotion(services);
        AddSsrClient(services);
        return services;
    }

    // The command line plans and opens a pass as the admin does.
    private static void AddPasses(IServiceCollection services)
    {
        services.TryAddSingleton<EpochCoverage>();
        services.TryAddSingleton<ExpectedPages>();
        services.TryAddSingleton<PassPlanning>();
        services.TryAddSingleton<PassLauncher>();
        services.TryAddSingleton<PassControl>();
    }

    private static void AddPromotion(IServiceCollection services)
    {
        services.TryAddSingleton<PromotedVersions>();
        services.TryAddSingleton<VersionEnqueuer>();
        services.TryAddSingleton<PrerenderReconciler>();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IPromotionListener, PrerenderPromotionListener>());
    }

    private static void AddEngine(IServiceCollection services)
    {
        services.TryAddSingleton<UnitQueue>();
        services.TryAddSingleton<SsrClient>();
        services.TryAddSingleton<RenderPacer>();
        services.TryAddSingleton<SsrOutage>();
        services.TryAddSingleton<BuildCopier>();
        services.TryAddSingleton<PageRunner>();
        services.TryAddSingleton<UnitRunner>();
        services.TryAddSingleton<PrerenderEngine>();
        services.TryAddSingleton<EnginePulse>();
        services.TryAddSingleton<UnitWaits>();
    }

    // Toward the SSR alone, on the stack network: no redirect followed (a 301 is a page the
    // pass skips), no cookie, bodies left gzip as the store keeps them, and no log line per
    // request, four million of them in a pass.
    private static void AddSsrClient(IServiceCollection services) =>
        services.AddHttpClient(SsrClient.ClientName)
            .ConfigureHttpClient(static (provider, client) =>
                Configure(client, provider.GetRequiredService<IOptions<PrerenderOptions>>().Value))
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.None,
            })
            .RemoveAllLoggers();

    // The pass renders each page as a visitor of the public host would get it: the SSR trusts
    // these two proxy headers for the origin of the canonical and hreflang links. The Host is
    // the public one as well, as nginx sends it: the SSR refuses any Host outside
    // LODB_ALLOWED_HOSTS (400), its own name on the stack network included.
    private static void Configure(HttpClient client, PrerenderOptions options)
    {
        client.Timeout = SsrTimeout;
        client.MaxResponseContentBufferSize = MaxAnswerBytes;
        if (!options.IsConfigured)
        {
            return;
        }

        client.BaseAddress = new Uri(options.SsrBaseUrl!);
        var headers = client.DefaultRequestHeaders;
        headers.Host = options.PublicHost;
        headers.TryAddWithoutValidation(
            PrerenderHeaders.Prerender,
            PrerenderHeaders.PrerenderValue);
        headers.TryAddWithoutValidation(PrerenderHeaders.ForwardedHost, options.PublicHost);
        headers.TryAddWithoutValidation(PrerenderHeaders.ForwardedProto, PrerenderHeaders.Https);
        headers.AcceptEncoding.ParseAdd(PrerenderHeaders.Gzip);
    }
}
