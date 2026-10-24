using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Robots.Detection;

/// <summary>
/// Follows the page requests of each client that sends a browser's user agent, and tells
/// when one goes past <see cref="RobotsOptions.UndeclaredThreshold"/> requests within
/// <see cref="RobotsOptions.UndeclaredWindow"/>: a reader asks nginx for the first page of a
/// visit, the application renders the next ones. Past it, the client stays a robot
/// <see cref="RobotsOptions.UndeclaredMemory"/>.
/// </summary>
/// <remarks>
/// The client is a keyed hash of its address and user agent, never the address itself. The
/// requests before the threshold are not kept: the hits of a hidden robot start at the one
/// that gives it away. At most <see cref="RobotsOptions.MaxTrackedClients"/> are followed.
/// Not thread safe: the journal's single pump is its only caller.
/// </remarks>
internal sealed class UndeclaredDetector(IOptions<RobotsOptions> options)
{
    private const int Halves = 2;

    private readonly Dictionary<string, Pace> _clients = new(StringComparer.Ordinal);

    /// <summary>Counts a page request of the client at <paramref name="at"/>.</summary>
    public void Count(string client, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(client);
        var settings = options.Value;
        if (!_clients.TryGetValue(client, out var pace))
        {
            MakeRoom(at, settings.MaxTrackedClients);
            pace = new Pace { WindowStart = at };
            _clients[client] = pace;
        }

        if (pace.FlaggedUntil > at)
        {
            return;
        }

        if (at - pace.WindowStart >= settings.UndeclaredWindow)
        {
            pace.WindowStart = at;
            pace.Requests = 0;
        }

        pace.Requests++;
        if (pace.Requests >= settings.UndeclaredThreshold)
        {
            pace.FlaggedUntil = at + settings.UndeclaredMemory;
        }
    }

    /// <summary>Whether the client is taken for a robot at <paramref name="at"/>.</summary>
    public bool IsRobot(string client, DateTimeOffset at) =>
        _clients.TryGetValue(client, out var pace) && pace.FlaggedUntil > at;

    // The clients done with, first; still full, the quieter half of the others.
    private void MakeRoom(DateTimeOffset at, int capacity)
    {
        if (_clients.Count < capacity)
        {
            return;
        }

        var window = options.Value.UndeclaredWindow;
        foreach (var done in _clients
            .Where(entry => entry.Value.FlaggedUntil <= at
                && at - entry.Value.WindowStart >= window)
            .Select(static entry => entry.Key)
            .ToList())
        {
            _clients.Remove(done);
        }

        if (_clients.Count < capacity)
        {
            return;
        }

        foreach (var quiet in _clients
            .Where(entry => entry.Value.FlaggedUntil <= at)
            .OrderBy(static entry => entry.Value.Requests)
            .Take(Math.Max(1, _clients.Count / Halves))
            .Select(static entry => entry.Key)
            .ToList())
        {
            _clients.Remove(quiet);
        }
    }

    private sealed class Pace
    {
        public DateTimeOffset WindowStart { get; set; }

        public int Requests { get; set; }

        public DateTimeOffset FlaggedUntil { get; set; } = DateTimeOffset.MinValue;
    }
}
