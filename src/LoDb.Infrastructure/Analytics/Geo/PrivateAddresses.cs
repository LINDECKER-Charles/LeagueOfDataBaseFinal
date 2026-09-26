using System.Net;

namespace LoDb.Infrastructure.Analytics.Geo;

/// <summary>
/// The addresses no database places, as Symfony's <c>IpUtils::isPrivateIp</c> lists them:
/// private, loopback, link-local and reserved ranges.
/// </summary>
/// <remarks>
/// An IPv4 address mapped into IPv6 is read as the IPv4 address it carries.
/// </remarks>
internal static class PrivateAddresses
{
    private static readonly IPNetwork[] Ranges =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("240.0.0.0/4"),
        IPNetwork.Parse("::/128"),
        IPNetwork.Parse("::1/128"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("fe80::/10"),
    ];

    public static bool Contains(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return Array.Exists(Ranges, range => range.Contains(normalized));
    }
}
