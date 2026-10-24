using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Robots.Verification;

/// <summary>
/// The reverse DNS of the addresses of a batch: those already known from memory, at most
/// <see cref="RobotsOptions.DnsLookupsPerBatch"/> new ones asked, a few at once, each within
/// <see cref="RobotsOptions.DnsTimeout"/>. The others wait for the next batches.
/// </summary>
/// <remarks>
/// An answer is kept <see cref="RobotsOptions.DnsCacheLifetime"/>, a failure
/// <see cref="RobotsOptions.DnsFailureLifetime"/>, at most
/// <see cref="RobotsOptions.DnsCacheSize"/> addresses. Not thread safe: the journal's single
/// pump is its only caller.
/// </remarks>
internal sealed class NetworkResolver(
    IReverseDns dns,
    IOptions<RobotsOptions> options,
    RobotsMetrics metrics,
    TimeProvider timeProvider)
{
    private const string Confirmed = "confirmed";
    private const string Unconfirmed = "unconfirmed";
    private const string NoName = "no_name";
    private const string Failed = "failed";
    private const int Halves = 2;

    private readonly Dictionary<IPAddress, (ReverseLookup Lookup, DateTimeOffset Until)> _known =
        [];

    /// <returns>The lookups known now; an address put off is missing from them.</returns>
    public async Task<IReadOnlyDictionary<IPAddress, ReverseLookup>> ResolveAsync(
        IReadOnlyCollection<IPAddress> addresses,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var found = new Dictionary<IPAddress, ReverseLookup>();
        var asked = new List<IPAddress>();
        foreach (var address in addresses.Distinct())
        {
            if (_known.TryGetValue(address, out var kept) && kept.Until > now)
            {
                found[address] = kept.Lookup;
            }
            else if (asked.Count < settings.DnsLookupsPerBatch)
            {
                asked.Add(address);
            }
        }

        foreach (var (address, lookup) in await AskAsync(asked, cancellationToken))
        {
            Remember(address, lookup, now);
            found[address] = lookup;
        }

        return found;
    }

    private async Task<ConcurrentDictionary<IPAddress, ReverseLookup>> AskAsync(
        List<IPAddress> addresses,
        CancellationToken cancellationToken)
    {
        var answers = new ConcurrentDictionary<IPAddress, ReverseLookup>();
        var parallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Value.DnsParallelism,
            CancellationToken = cancellationToken,
        };
        await Parallel.ForEachAsync(addresses, parallel, async (address, token) =>
            answers[address] = await LookupAsync(address, token));
        return answers;
    }

    private async Task<ReverseLookup> LookupAsync(IPAddress address, CancellationToken stopping)
    {
        using var deadline = new CancellationTokenSource(options.Value.DnsTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            stopping,
            deadline.Token);
        try
        {
            var lookup = await LookupWithinAsync(address, linked.Token);
            metrics.RecordLookup(OutcomeOf(lookup));
            return lookup;
        }
        catch (Exception exception) when (exception is SocketException
            || (exception is OperationCanceledException && !stopping.IsCancellationRequested))
        {
            metrics.RecordLookup(Failed);
            return ReverseLookup.Failed;
        }
    }

    private async Task<ReverseLookup> LookupWithinAsync(
        IPAddress address,
        CancellationToken cancellationToken)
    {
        var name = await dns.NameOfAsync(address, cancellationToken);
        if (name is null)
        {
            return ReverseLookup.NoName;
        }

        var host = name.TrimEnd('.').ToLowerInvariant();
        var back = await dns.AddressesOfAsync(host, cancellationToken);
        var own = Canonical(address);
        return new ReverseLookup
        {
            Host = host,
            IsConfirmed = back.Any(resolved => Canonical(resolved).Equals(own)),
        };
    }

    private void Remember(IPAddress address, ReverseLookup lookup, DateTimeOffset now)
    {
        var settings = options.Value;
        if (_known.Count >= settings.DnsCacheSize)
        {
            Evict(now, settings.DnsCacheSize);
        }

        var lifetime = lookup.HasFailed ? settings.DnsFailureLifetime : settings.DnsCacheLifetime;
        _known[address] = (lookup, now + lifetime);
    }

    // The expired ones first; still full, the half closest to expiring.
    private void Evict(DateTimeOffset now, int capacity)
    {
        foreach (var expired in _known.Where(entry => entry.Value.Until <= now).ToList())
        {
            _known.Remove(expired.Key);
        }

        if (_known.Count < capacity)
        {
            return;
        }

        foreach (var entry in _known.OrderBy(static entry => entry.Value.Until)
            .Take(_known.Count / Halves).ToList())
        {
            _known.Remove(entry.Key);
        }
    }

    private static IPAddress Canonical(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static string OutcomeOf(ReverseLookup lookup) =>
        lookup.Host is null ? NoName : lookup.IsConfirmed ? Confirmed : Unconfirmed;
}
