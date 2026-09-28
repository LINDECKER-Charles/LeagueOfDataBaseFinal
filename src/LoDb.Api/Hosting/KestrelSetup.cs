using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Hosting;

/// <summary>
/// Kestrel listeners: the API port and the metrics port, from <see cref="HostingOptions"/>.
/// </summary>
internal static class KestrelSetup
{
    private const string PortsMustDiffer = "LoDb:Hosting:HttpPort and MetricsPort must differ.";
    private const string NetworksMustParse =
        "LoDb:Hosting:KnownProxyNetworks must only hold CIDR networks.";

    public static WebApplicationBuilder AddLoDbKestrel(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<HostingOptions>()
            .BindConfiguration(HostingOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(static options => options.HttpPort != options.MetricsPort, PortsMustDiffer)
            .Validate(
                static options => options.KnownProxyNetworks.All(IsNetwork),
                NetworksMustParse)
            .ValidateOnStart();
        builder.Services.AddOptions<KestrelServerOptions>()
            .Configure<IOptions<HostingOptions>>(static (kestrel, hosting) =>
                Listen(kestrel, hosting.Value));

        // The explicit listeners replace ASPNETCORE_URLS and ASPNETCORE_HTTP_PORTS (set by the
        // base image): clearing them keeps Kestrel from warning that it overrides them.
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, string.Empty);
        builder.WebHost.UseSetting(WebHostDefaults.HttpPortsKey, string.Empty);
        builder.WebHost.UseSetting(WebHostDefaults.HttpsPortsKey, string.Empty);
        return builder;
    }

    private static void Listen(KestrelServerOptions kestrel, HostingOptions hosting)
    {
        kestrel.ListenAnyIP(hosting.HttpPort);
        kestrel.ListenAnyIP(hosting.MetricsPort);
    }

    private static bool IsNetwork(string network) => System.Net.IPNetwork.TryParse(network, out _);
}
