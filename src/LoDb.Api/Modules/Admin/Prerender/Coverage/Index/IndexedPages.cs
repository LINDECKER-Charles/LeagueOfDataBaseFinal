using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage.Index;

/// <summary>
/// The pages the index of states expects of each version it covers, for the admin: computed
/// from the snapshot alone, in memory, in one sweep of its states. A version out of the index
/// has none (its pages follow the residual rule, all revisable). The lists and the patch page
/// of an older patch are frozen too: they are not counted here.
/// </summary>
internal static class IndexedPages
{
    /// <summary>The counts of every covered version of <paramref name="snapshot"/>.</summary>
    public static IReadOnlyDictionary<PatchVersion, IndexedPageCounts> Of(
        EntityStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var versions = snapshot.Coverage.All;
        var positions = versions.Select(static (version, position) => (version, position))
            .ToDictionary(static pair => pair.version, static pair => pair.position);
        var frozen = new int[versions.Count + 1];
        var revisable = new int[versions.Count + 1];
        foreach (var state in snapshot.States)
        {
            Spread(state, positions, state.IsOpen ? revisable : frozen);
        }

        var (closedHere, openHere) = (0, 0);
        var counts = new Dictionary<PatchVersion, IndexedPageCounts>();
        for (var position = 0; position < versions.Count; position++)
        {
            closedHere += frozen[position];
            openHere += revisable[position];
            counts[versions[position]] = new IndexedPageCounts(
                OpenersOf(snapshot, versions[position]),
                closedHere,
                openHere);
        }

        return counts;
    }

    // One more page from the state's opener, one less past its last version: the running sum
    // over the versions counts the states that hold each one.
    private static void Spread(
        IndexedState state,
        Dictionary<PatchVersion, int> positions,
        int[] steps)
    {
        if (!positions.TryGetValue(state.Opener, out var from))
        {
            return;
        }

        var until = state.Last is { } last && positions.TryGetValue(last, out var end)
            ? end
            : positions.Count - 1;
        steps[from]++;
        steps[until + 1]--;
    }

    private static int OpenersOf(EntityStateSnapshot snapshot, PatchVersion version) =>
        snapshot.OpenedAt(version).Count + (snapshot.HasPatchPage(version) ? 1 : 0);
}
