using System.ComponentModel.DataAnnotations;
using System.Net;

namespace LoDb.Api.Hosting;

/// <summary>
/// Network settings of the host, bound from <c>LoDb:Hosting</c>.
/// </summary>
internal sealed class HostingOptions
{
    public const string SectionName = "LoDb:Hosting";
    public const string HttpPortKey = SectionName + ":" + nameof(HttpPort);
    public const int DefaultHttpPort = 8080;
    public const int DefaultMetricsPort = 9464;
    private const int LowestPort = 1;

    /// <summary>Port of the API, the only one nginx reaches.</summary>
    [Range(LowestPort, IPEndPoint.MaxPort)]
    public int HttpPort { get; set; } = DefaultHttpPort;

    /// <summary>Port of the Prometheus scrape, never routed by nginx nor published.</summary>
    [Range(LowestPort, IPEndPoint.MaxPort)]
    public int MetricsPort { get; set; } = DefaultMetricsPort;

    /// <summary>
    /// CIDR networks of the reverse proxy whose <c>X-Forwarded-*</c> headers are trusted.
    /// </summary>
    /// <remarks>Empty: only loopback proxies are trusted.</remarks>
    public IReadOnlyList<string> KnownProxyNetworks { get; set; } = [];
}
