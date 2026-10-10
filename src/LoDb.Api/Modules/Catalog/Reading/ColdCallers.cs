using System.Net;
using LoDb.Ingestion.Catalog;
using Microsoft.AspNetCore.HttpOverrides;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>
/// Tells the server-side renderer's calls from the others, by their connection alone: no
/// header a client could write decides it.
/// </summary>
/// <remarks>
/// The renderer calls the API directly over the stack's private network, without any
/// forwarding header. Every call through nginx carries <c>X-Forwarded-For</c>, which nginx
/// writes itself (<c>snippets/proxy-headers.conf</c>); once the forwarded headers of a
/// trusted proxy are applied, it has become <c>X-Original-For</c>. So a call is internal only
/// from a private address and with neither header. A connection without an address is an
/// in-process host, such as the tests'.
/// </remarks>
internal static class ColdCallers
{
    private static readonly System.Net.IPNetwork[] PrivateNetworks =
    [
        System.Net.IPNetwork.Parse("10.0.0.0/8"),
        System.Net.IPNetwork.Parse("172.16.0.0/12"),
        System.Net.IPNetwork.Parse("192.168.0.0/16"),
        System.Net.IPNetwork.Parse("127.0.0.0/8"),
        System.Net.IPNetwork.Parse("::1/128"),
        System.Net.IPNetwork.Parse("fc00::/7"),
    ];

    public static ColdCaller Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var headers = context.Request.Headers;
        if (headers.ContainsKey(ForwardedHeadersDefaults.XForwardedForHeaderName)
            || headers.ContainsKey(ForwardedHeadersDefaults.XOriginalForHeaderName))
        {
            return ColdCaller.Public;
        }

        return context.Connection.RemoteIpAddress is not { } address || IsPrivate(address)
            ? ColdCaller.Internal
            : ColdCaller.Public;
    }

    private static bool IsPrivate(IPAddress address)
    {
        var plain = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return PrivateNetworks.Any(network => network.Contains(plain));
    }
}
