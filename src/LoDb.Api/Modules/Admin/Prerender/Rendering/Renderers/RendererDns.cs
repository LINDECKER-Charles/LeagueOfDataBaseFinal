using System.Net;
using System.Net.Sockets;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering.Renderers;

/// <summary>
/// The stack network's DNS: Docker answers every replica of a service under the alias they
/// share, IPv4 only on these networks.
/// </summary>
internal sealed class RendererDns : IRendererDns
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(
                host,
                AddressFamily.InterNetwork,
                cancellationToken);
            return [.. addresses.OrderBy(
                static address => address.ToString(),
                StringComparer.Ordinal)];
        }
        catch (SocketException)
        {
            return [];
        }
    }
}
