using LoDb.Domain.Versions;
using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Queue;

/// <summary>
/// Images to settle while someone watches them land: the loader of a patch switch.
/// </summary>
public sealed record WatchedImages
{
    public required PatchVersion Version { get; init; }

    public required IReadOnlyCollection<DdragonImage> Images { get; init; }

    /// <summary>
    /// Told of each image this call fetched, from the fetching threads: it must be
    /// thread-safe and never block.
    /// </summary>
    public required IProgress<DdragonImage> Progress { get; init; }
}
