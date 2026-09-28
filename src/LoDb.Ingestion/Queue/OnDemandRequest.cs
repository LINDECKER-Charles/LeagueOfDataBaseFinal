using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Queue;

/// <summary>Work queued for the on-demand worker: datasets, images, or both.</summary>
public sealed record OnDemandRequest
{
    public required PatchVersion Version { get; init; }

    /// <summary>
    /// The language whose four datasets are needed; <see langword="null"/> for images only.
    /// </summary>
    public DdragonLanguage? Language { get; init; }

    /// <summary>The images to settle: present with a blob, or absent.</summary>
    public IReadOnlyCollection<DdragonImage> Images { get; init; } = [];

    public OnDemandOrigin Origin { get; init; }
}
