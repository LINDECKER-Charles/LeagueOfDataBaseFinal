using System.Net;
using System.Net.Sockets;
using LoDb.Infrastructure.Robots.Verification;

namespace LoDb.Infrastructure.Tests.Robots.Support;

/// <summary>
/// A resolver of fixed answers: a name per address, the addresses of each name, the addresses
/// whose lookup fails; it counts the reverse questions it was asked.
/// </summary>
internal sealed class FakeReverseDns : IReverseDns
{
    private readonly Dictionary<IPAddress, string> _names = [];
    private readonly Dictionary<string, IPAddress[]> _addresses = new(StringComparer.Ordinal);
    private readonly HashSet<IPAddress> _failing = [];

    public int ReverseQuestions { get; private set; }

    /// <summary>The address reverses to the name, which resolves back to it.</summary>
    public FakeReverseDns Confirmed(string address, string host)
    {
        var ip = IPAddress.Parse(address);
        _names[ip] = host;
        _addresses[host] = [ip];
        return this;
    }

    public FakeReverseDns Failing(string address)
    {
        _failing.Add(IPAddress.Parse(address));
        return this;
    }

    public Task<string?> NameOfAsync(IPAddress address, CancellationToken cancellationToken)
    {
        ReverseQuestions++;
        if (_failing.Contains(address))
        {
            throw new SocketException((int)SocketError.TryAgain);
        }

        return Task.FromResult(_names.GetValueOrDefault(address));
    }

    public Task<IReadOnlyList<IPAddress>> AddressesOfAsync(
        string host,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IPAddress>>(_addresses.GetValueOrDefault(host) ?? []);
}
