using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Images;

/// <summary>Images of one version to settle in one run of <see cref="ImageIngestion"/>.</summary>
/// <param name="Version">The version whose manifest records the verdicts.</param>
/// <param name="Images">The images; a manifest key given twice is fetched once.</param>
/// <param name="Force">
/// Fetch the recorded images again and replace their row when it changed, instead of
/// skipping them.
/// </param>
internal sealed record ImageBatch(
    PatchVersion Version,
    IReadOnlyCollection<DdragonImage> Images,
    bool Force)
{
    /// <summary>
    /// Told of each image the run fetched, whatever its verdict, before the verdicts of its
    /// chunk are recorded. An image skipped as already recorded is not reported.
    /// </summary>
    public IProgress<DdragonImage>? Progress { get; init; }
}
