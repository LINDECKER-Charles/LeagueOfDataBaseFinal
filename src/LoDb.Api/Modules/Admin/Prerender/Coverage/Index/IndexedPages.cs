using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage.Index;

/// <summary>
/// The pages the index of states expects of each version it covers, for the admin: computed
/// from the snapshot alone, in memory. A version out of the index has none (its pages follow
/// the residual rule).
/// </summary>
internal static class IndexedPages
{
    /// <summary>The counts of every covered version of <paramref name="snapshot"/>.</summary>
    public static IReadOnlyDictionary<PatchVersion, IndexedPageCounts> Of(
        EntityStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Coverage.All.ToDictionary(
            static version => version,
            version => new IndexedPageCounts(OpenersOf(snapshot, version)));
    }

    private static int OpenersOf(EntityStateSnapshot snapshot, PatchVersion version) =>
        snapshot.OpenedAt(version).Count + (snapshot.HasPatchPage(version) ? 1 : 0);
}
