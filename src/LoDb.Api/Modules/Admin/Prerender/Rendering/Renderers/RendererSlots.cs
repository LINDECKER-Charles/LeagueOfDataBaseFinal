using System.Net;

namespace LoDb.Api.Modules.Admin.Prerender.Rendering.Renderers;

/// <summary>
/// The requests the pass has in flight at each renderer, and the choice of the next one: the
/// least busy, the one after the last chosen among equals, so that equal renderers take turns.
/// </summary>
/// <remarks>
/// A unit's first pages load its catalog into whichever renderer they reach, slowly: the least
/// busy rule sends the next ones elsewhere instead of queuing them behind it.
/// </remarks>
internal sealed class RendererSlots
{
    private readonly Lock _gate = new();
    private readonly Dictionary<IPAddress, int> _inFlight = [];
    private int _turn;

    /// <summary>Takes a slot at the least busy of <paramref name="addresses"/>.</summary>
    public IPAddress Take(IReadOnlyList<IPAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentOutOfRangeException.ThrowIfZero(addresses.Count);
        lock (_gate)
        {
            var start = _turn++ % addresses.Count;
            var chosen = Enumerable.Range(0, addresses.Count)
                .Select(offset => addresses[(start + offset) % addresses.Count])
                .MinBy(address => _inFlight.GetValueOrDefault(address))!;
            _inFlight[chosen] = _inFlight.GetValueOrDefault(chosen) + 1;
            return chosen;
        }
    }

    /// <summary>Gives back the slot taken at <paramref name="address"/>.</summary>
    public void Release(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        lock (_gate)
        {
            var left = _inFlight.GetValueOrDefault(address) - 1;
            if (left > 0)
            {
                _inFlight[address] = left;
            }
            else
            {
                _inFlight.Remove(address);
            }
        }
    }
}
