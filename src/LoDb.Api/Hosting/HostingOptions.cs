using System.ComponentModel.DataAnnotations;
using System.Net;

namespace LoDb.Api.Hosting;

/// <summary>
/// Network and request settings of the host, bound from <c>LoDb:Hosting</c>.
/// </summary>
internal sealed class HostingOptions
{
    public const string SectionName = "LoDb:Hosting";
    public const string HttpPortKey = SectionName + ":" + nameof(HttpPort);
    public const int DefaultHttpPort = 8080;
    public const int DefaultMetricsPort = 9464;

    /// <summary>
    /// About twice the largest body the site sends: a build with every text at its longest,
    /// each character escaped (measured by the builds end-to-end suite).
    /// </summary>
    public const int DefaultMaxRequestBodySize = 128 * 1024;

    private const int LowestPort = 1;
    private const int SmallestRequestBodySize = 1;
    private const int DefaultDatabaseBusySeconds = 5;
    private const string ShortestRetryDelay = "00:00:01";
    private const string LongestRetryDelay = "00:05:00";

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

    /// <summary>
    /// Largest request body Kestrel reads, in bytes; a larger one is answered 413. An endpoint
    /// that needs more declares its own <c>RequestSizeLimit</c>, which wins over this one.
    /// </summary>
    [Range(SmallestRequestBodySize, int.MaxValue)]
    public int MaxRequestBodySize { get; set; } = DefaultMaxRequestBodySize;

    /// <summary>
    /// <c>Retry-After</c> of the 503 a request gets when no database connection freed up in
    /// time: about the pool's own wait, so that a retry does not queue behind the same backlog.
    /// </summary>
    [Range(typeof(TimeSpan), ShortestRetryDelay, LongestRetryDelay)]
    public TimeSpan DatabaseBusyRetryAfter { get; set; } =
        TimeSpan.FromSeconds(DefaultDatabaseBusySeconds);
}
