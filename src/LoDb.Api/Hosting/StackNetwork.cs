using System.Net;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Hosting;

/// <summary>
/// The peers of the stack itself: its private networks (<c>LoDb:Hosting:KnownProxyNetworks</c>,
/// the subnet of nginx and the renderer on a served host) and the loopback.
/// </summary>
/// <remarks>
/// The same networks decide whose <c>X-Forwarded-*</c> headers are trusted, who may reach the
/// API port at all (<see cref="NetworkGate"/>) and which direct calls are the renderer's. A
/// connection without an address is an in-process host, such as the tests': it has no socket
/// any other process could open.
/// </remarks>
internal sealed class StackNetwork(IOptions<HostingOptions> options)
{
    private readonly System.Net.IPNetwork[] _networks =
        [.. options.Value.KnownProxyNetworks.Select(static network =>
            System.Net.IPNetwork.Parse(network))];

    public bool Contains(IPAddress? address)
    {
        if (address is null)
        {
            return true;
        }

        var plain = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return IPAddress.IsLoopback(plain) || _networks.Any(network => network.Contains(plain));
    }
}
