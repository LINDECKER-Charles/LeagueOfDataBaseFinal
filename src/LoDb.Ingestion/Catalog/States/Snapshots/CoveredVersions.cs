using System.Collections.Frozen;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Snapshots;

/// <summary>
/// The versions a generation covers, oldest first: a run of consecutive listed
/// versions ending at the latest promoted one when the index is up to date.
/// </summary>
public sealed class CoveredVersions
{
    private readonly PatchVersion[] versions;
    private readonly FrozenDictionary<PatchVersion, int> positions;

    public CoveredVersions(IEnumerable<PatchVersion> oldestFirst)
    {
        ArgumentNullException.ThrowIfNull(oldestFirst);
        versions = [.. oldestFirst];
        positions = versions
            .Select(static (version, position) => (version, position))
            .ToFrozenDictionary(static pair => pair.version, static pair => pair.position);
    }

    public static CoveredVersions None { get; } = new([]);

    /// <summary>Every covered version, oldest first.</summary>
    public IReadOnlyList<PatchVersion> All => versions;

    /// <summary>The oldest covered version, which has no patch page; null when none is.</summary>
    public PatchVersion? Start => versions.Length > 0 ? versions[0] : null;

    /// <summary>The newest covered version; null when none is.</summary>
    public PatchVersion? End => versions.Length > 0 ? versions[^1] : null;

    public bool Contains(PatchVersion version) => positions.ContainsKey(version);

    /// <summary>The covered version before a covered one; null for the start or outside.</summary>
    public PatchVersion? Before(PatchVersion version) =>
        positions.TryGetValue(version, out var position) && position > 0
            ? versions[position - 1]
            : null;

    /// <summary>The covered version after a covered one; null for the end or outside.</summary>
    public PatchVersion? After(PatchVersion version) =>
        positions.TryGetValue(version, out var position) && position < versions.Length - 1
            ? versions[position + 1]
            : null;
}
