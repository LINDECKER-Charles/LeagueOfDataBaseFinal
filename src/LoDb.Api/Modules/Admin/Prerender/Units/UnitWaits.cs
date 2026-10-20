using System.Collections.Concurrent;

namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>
/// Since when the units of a version wait in a pass: for a catalog not stored yet, or for
/// every server to see the version as an older patch. Past <see cref="Longest"/>, they fail.
/// </summary>
/// <remarks>
/// A wait is per version, its catalog and its rank being the same in every locale. Held in
/// memory: a restart starts the count again, which only delays the end of a wait.
/// </remarks>
internal sealed class UnitWaits(TimeProvider clock)
{
    /// <summary>
    /// How long a version may wait: an ingestion queued for it is done well within, an
    /// ingestion that keeps failing is not.
    /// </summary>
    public static readonly TimeSpan Longest = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<(long PassId, string Version), long> _since = new();

    /// <summary>
    /// Starts the wait of <paramref name="unit"/>'s version; false if it waits already.
    /// </summary>
    public bool TryBegin(ClaimedUnit unit) => _since.TryAdd(KeyOf(unit), clock.GetTimestamp());

    /// <summary>
    /// Whether the version of <paramref name="unit"/> waited past <see cref="Longest"/>.
    /// </summary>
    public bool HasExpired(ClaimedUnit unit) =>
        _since.TryGetValue(KeyOf(unit), out var since) && clock.GetElapsedTime(since) >= Longest;

    /// <summary>Ends the wait of <paramref name="unit"/>'s version, if it waited.</summary>
    public void End(ClaimedUnit unit) => _since.TryRemove(KeyOf(unit), out _);

    private static (long, string) KeyOf(ClaimedUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return (unit.Pass.Id, unit.Version.Value);
    }
}
