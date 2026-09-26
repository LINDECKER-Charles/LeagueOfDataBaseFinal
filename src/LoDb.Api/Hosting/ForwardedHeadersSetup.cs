using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Hosting;

/// <summary>
/// Client address and scheme restored from the reverse proxy (Caddy, then nginx).
/// </summary>
/// <remarks>
/// Only nginx's network is trusted: every per-IP limit, the visitor hash, the geolocation
/// and the audit trail depend on this address.
/// </remarks>
internal static class ForwardedHeadersSetup
{
    public static IServiceCollection AddLoDbForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<HostingOptions>>(static (forwarded, hosting) =>
                Apply(forwarded, hosting.Value));
        return services;
    }

    private static void Apply(ForwardedHeadersOptions forwarded, HostingOptions hosting)
    {
        forwarded.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Without a configured network the loopback defaults stay: emptying both lists would
        // make the middleware trust every caller.
        if (hosting.KnownProxyNetworks.Count == 0)
        {
            return;
        }

        forwarded.KnownIPNetworks.Clear();
        forwarded.KnownProxies.Clear();
        foreach (var network in hosting.KnownProxyNetworks)
        {
            forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }
    }
}
