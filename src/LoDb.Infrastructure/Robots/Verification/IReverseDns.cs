using System.Net;

namespace LoDb.Infrastructure.Robots.Verification;

/// <summary>The two DNS questions of a robot's verification.</summary>
internal interface IReverseDns
{
    /// <returns>The name the address reverses to; null when it reverses to none.</returns>
    /// <exception cref="System.Net.Sockets.SocketException">The lookup failed.</exception>
    Task<string?> NameOfAsync(IPAddress address, CancellationToken cancellationToken);

    /// <returns>The addresses of the name; none when it has none.</returns>
    /// <exception cref="System.Net.Sockets.SocketException">The lookup failed.</exception>
    Task<IReadOnlyList<IPAddress>> AddressesOfAsync(
        string host,
        CancellationToken cancellationToken);
}
