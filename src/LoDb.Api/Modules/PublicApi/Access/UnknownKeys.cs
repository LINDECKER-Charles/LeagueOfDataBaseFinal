using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.PublicApi.Access;

/// <summary>
/// The fingerprints no key has, remembered so that a caller repeating a made-up key costs
/// one read of the database per lifetime rather than one per request.
/// </summary>
/// <remarks>
/// Bounded like go-api's cache: past <see cref="MaxEntries"/> the expired entries go, and if
/// the live ones still fill it, a new one is not remembered. Random keys then cost a read
/// each, never unbounded memory. Each entry carries the generation of the directory it was
/// found unknown in, so an answer read before a change never outlives it.
/// </remarks>
internal sealed class UnknownKeys(TimeProvider timeProvider, IOptions<PublicApiOptions> options)
{
    public const int MaxEntries = 50_000;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeSpan _lifetime = options.Value.KeyCacheLifetime;

    public bool Contains(string hash, long generation)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(hash, out var entry))
            {
                return false;
            }

            if (entry.Generation == generation && timeProvider.GetUtcNow() < entry.ExpiresAt)
            {
                return true;
            }

            _entries.Remove(hash);
            return false;
        }
    }

    public void Add(string hash, long generation)
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (!_entries.ContainsKey(hash) && _entries.Count >= MaxEntries)
            {
                DropExpired(now);
                if (_entries.Count >= MaxEntries)
                {
                    return;
                }
            }

            _entries[hash] = new Entry(generation, now + _lifetime);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    private void DropExpired(DateTimeOffset now)
    {
        foreach (var (hash, entry) in _entries)
        {
            if (entry.ExpiresAt <= now)
            {
                _entries.Remove(hash);
            }
        }
    }

    private readonly record struct Entry(long Generation, DateTimeOffset ExpiresAt);
}
