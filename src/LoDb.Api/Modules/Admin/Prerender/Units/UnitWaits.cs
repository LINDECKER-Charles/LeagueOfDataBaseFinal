using System.Collections.Concurrent;
using LoDb.Api.Modules.Admin.Prerender.Units.Waits;

namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>
/// Since when the units of a version wait in a pass: for a catalog not stored yet, or for
/// every server to see the version as an older patch. Past <see cref="Longest"/>, they fail.
/// </summary>
/// <remarks>
/// A wait is per version, its catalog and its rank being the same in every locale. Held in
/// memory: a restart starts the count again, which only delays the end of a wait, and another
/// instance sees none of them. The admin's monitor reads them (<see cref="Snapshot"/>).
/// </remarks>
internal sealed class UnitWaits(TimeProvider clock)
{
    /// <summary>
    /// How long a version may wait: an ingestion queued for it is done well within, an
    /// ingestion that keeps failing is not.
    /// </summary>
    public static readonly TimeSpan Longest = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<(long PassId, string Version), Wait> _waits = new();

    /// <summary>
    /// Starts the wait of <paramref name="unit"/>'s version for <paramref name="reason"/>;
    /// false if it waits already, its reason then brought up to date.
    /// </summary>
    public bool TryBegin(ClaimedUnit unit, UnitWaitReason reason)
    {
        var isNew = false;
        _waits.AddOrUpdate(
            KeyOf(unit),
            _ =>
            {
                isNew = true;
                return new Wait(clock.GetTimestamp(), clock.GetUtcNow(), reason);
            },
            (_, wait) => wait with { Reason = reason });
        return isNew;
    }

    /// <summary>
    /// Whether the version of <paramref name="unit"/> waited past <see cref="Longest"/>.
    /// </summary>
    public bool HasExpired(ClaimedUnit unit) =>
        _waits.TryGetValue(KeyOf(unit), out var wait)
        && clock.GetElapsedTime(wait.Timestamp) >= Longest;

    /// <summary>Ends the wait of <paramref name="unit"/>'s version, if it waited.</summary>
    public void End(ClaimedUnit unit) => _waits.TryRemove(KeyOf(unit), out _);

    /// <summary>The versions of <paramref name="passId"/> that wait on this instance.</summary>
    public IReadOnlyList<UnitWait> Snapshot(long passId) =>
    [
        .. _waits.Where(wait => wait.Key.PassId == passId)
            .Select(static wait =>
                new UnitWait(wait.Key.Version, wait.Value.Reason, wait.Value.Since))
            .OrderBy(static wait => wait.Since),
    ];

    private static (long, string) KeyOf(ClaimedUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return (unit.Pass.Id, unit.Version.Value);
    }

    // The monotonic start counts the wait; the wall-clock one is what the admin shows.
    private sealed record Wait(long Timestamp, DateTimeOffset Since, UnitWaitReason Reason);
}
