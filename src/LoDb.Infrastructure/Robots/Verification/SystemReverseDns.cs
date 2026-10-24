using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Robots.Verification;

/// <summary>
/// <see cref="IReverseDns"/> through the system's resolver, Docker's own in a container. A
/// name or an address that does not exist is an answer, never a failure.
/// </summary>
/// <remarks>
/// The system's resolver cannot always be cancelled: a question given up on still holds its
/// thread until the resolver answers. At most <see cref="RobotsOptions.DnsParallelism"/>
/// questions are in flight, given up on or not, so that a resolver that stops answering never
/// takes more threads than that; a question that finds no slot before its deadline fails.
/// </remarks>
internal sealed class SystemReverseDns(IOptions<RobotsOptions> options) : IReverseDns, IDisposable
{
    private readonly SemaphoreSlim _inFlight = new(
        options.Value.DnsParallelism,
        options.Value.DnsParallelism);

    public async Task<string?> NameOfAsync(IPAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        try
        {
            // An address given as text is looked up in reverse.
            var entry = await AskAsync(
                token => Dns.GetHostEntryAsync(address.ToString(), token),
                cancellationToken);
            var host = entry.HostName;
            return string.IsNullOrEmpty(host) || IPAddress.TryParse(host, out _) ? null : host;
        }
        catch (SocketException exception) when (IsAbsent(exception))
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<IPAddress>> AddressesOfAsync(
        string host,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        try
        {
            return await AskAsync(
                token => Dns.GetHostAddressesAsync(host, token),
                cancellationToken);
        }
        catch (SocketException exception) when (IsAbsent(exception))
        {
            return [];
        }
    }

    public void Dispose() => _inFlight.Dispose();

    // The slot is freed when the resolver answers, not when the caller stops waiting.
    private async Task<T> AskAsync<T>(
        Func<CancellationToken, Task<T>> question,
        CancellationToken cancellationToken)
    {
        await _inFlight.WaitAsync(cancellationToken);
        Task<T> asked;
        try
        {
            asked = question(cancellationToken);
        }
        catch
        {
            _inFlight.Release();
            throw;
        }

        _ = asked.ContinueWith(
            _ => _inFlight.Release(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return await asked.WaitAsync(cancellationToken);
    }

    private static bool IsAbsent(SocketException exception) =>
        exception.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData;
}
