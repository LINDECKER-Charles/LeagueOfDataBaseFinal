using LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage;

/// <summary>
/// The newer epoch ready to serve: the newest one above the served epoch, that the active pass
/// does not write, with no gap left in its coverage, whatever the passes that filled it.
/// </summary>
/// <remarks>
/// Units failed for good (<c>f</c>, a version Data Dragon does not list, a catalog that never
/// came) are admitted: the worker never takes them again, so asking for them would keep the
/// epoch from being ready for good. The SSR serves them, as it serves the failed pages of the
/// completed units; the panel counts both.
/// </remarks>
internal static class EpochReadiness
{
    /// <summary>
    /// The epoch to serve next among <paramref name="coverages"/>; null when none is ready.
    /// </summary>
    /// <param name="coverages">The coverage of each epoch not retired.</param>
    /// <param name="servedEpoch">The epoch nginx serves.</param>
    /// <param name="writingEpoch">
    /// The epoch the active pass writes; null while none is active.
    /// </param>
    public static int? Of(
        IReadOnlyDictionary<int, PrerenderEpochCoverage> coverages,
        int servedEpoch,
        int? writingEpoch)
    {
        ArgumentNullException.ThrowIfNull(coverages);
        return coverages
            .Where(epoch => epoch.Key > servedEpoch
                && epoch.Key != writingEpoch
                && IsReady(epoch.Value))
            .Select(static epoch => (int?)epoch.Key)
            .Max();
    }

    /// <summary>Whether <paramref name="coverage"/> has an older patch and no gap left.</summary>
    public static bool IsReady(PrerenderEpochCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        return coverage.ExpectedUnits > 0 && coverage.GapUnits == 0;
    }
}
