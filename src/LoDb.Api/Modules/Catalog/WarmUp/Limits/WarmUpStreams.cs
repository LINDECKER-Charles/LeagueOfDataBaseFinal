using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Catalog.WarmUp.Limits;

/// <summary>
/// The warm-up streams open on the instance, in all and per client
/// (<c>LoDb:Catalog:WarmUp:MaxStreams</c>, <c>MaxStreamsPerClient</c>).
/// </summary>
/// <remarks>
/// A client is an address, or an IPv6 /64, as the rate limits count it. A loader that finds
/// no place gets one failed frame at once, and visits the page anyway.
/// </remarks>
internal sealed class WarmUpStreams(IOptions<WarmUpOptions> options)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, int> perClient = new(StringComparer.Ordinal);
    private int open;

    /// <summary>A place for one more stream of the client, released when disposed.</summary>
    /// <returns>
    /// The place; <see langword="null"/> when the instance or the client has none left.
    /// </returns>
    public IDisposable? TryOpen(string client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var limits = options.Value;
        lock (gate)
        {
            var mine = perClient.GetValueOrDefault(client);
            if (open >= limits.MaxStreams || mine >= limits.MaxStreamsPerClient)
            {
                return null;
            }

            open++;
            perClient[client] = mine + 1;
        }

        return new Place(this, client);
    }

    private void Close(string client)
    {
        lock (gate)
        {
            open--;
            var left = perClient[client] - 1;
            if (left == 0)
            {
                perClient.Remove(client);
            }
            else
            {
                perClient[client] = left;
            }
        }
    }

    private sealed class Place(WarmUpStreams streams, string client) : IDisposable
    {
        private int closed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) == 0)
            {
                streams.Close(client);
            }
        }
    }
}
