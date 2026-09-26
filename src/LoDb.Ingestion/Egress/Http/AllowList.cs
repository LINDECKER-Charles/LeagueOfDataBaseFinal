using System.Collections.Frozen;
using LoDb.Ingestion.Egress.Errors;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Egress.Http;

/// <summary>
/// The SSRF policy: https only, and only the configured hosts, whatever the port.
/// </summary>
internal sealed class AllowList(IOptions<EgressOptions> options)
{
    private readonly FrozenSet<string> hosts =
        Normalize(options.Value.AllowedHosts).ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The configured hosts, trimmed, blanks and duplicates removed.
    /// </summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> configured) =>
    [
        .. configured
            .Select(static host => host.Trim())
            .Where(static host => host.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>
    /// The host to report for a URL, null when it has none.
    /// </summary>
    public static string? HostOf(Uri? url) => url is { IsAbsoluteUri: true } ? url.IdnHost : null;

    /// <summary>
    /// Why the URL is refused, null when it may be fetched.
    /// </summary>
    public EgressRefusal? Check(Uri? url)
    {
        if (url is not { IsAbsoluteUri: true })
        {
            return EgressRefusal.NotAbsolute;
        }

        if (!string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return EgressRefusal.Scheme;
        }

        return hosts.Contains(url.IdnHost) ? null : EgressRefusal.Host;
    }

    /// <summary>
    /// Throws <see cref="EgressRefusedException"/> when the URL is refused.
    /// </summary>
    public void Enforce(Uri? url)
    {
        if (Check(url) is { } reason)
        {
            throw new EgressRefusedException(reason, HostOf(url));
        }
    }
}
