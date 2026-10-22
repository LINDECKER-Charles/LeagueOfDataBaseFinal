using LoDb.Domain.Catalog;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Storage;

/// <summary>What a generation holds, for the summary line of an update.</summary>
internal sealed record GenerationSummary
{
    public required long GenerationId { get; init; }

    public PatchVersion? CoverageStart { get; init; }

    public PatchVersion? CoverageEnd { get; init; }

    public required int States { get; init; }

    public required int OpenStates { get; init; }

    /// <summary>The states of each resource, open and closed.</summary>
    public required IReadOnlyDictionary<ResourceType, int> ByResource { get; init; }
}
