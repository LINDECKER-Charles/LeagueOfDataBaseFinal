using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Builds.Import;

/// <summary>A structure carried over to another patch, and what the move changed.</summary>
public sealed record BuildProjection
{
    public required BuildStructure Structure { get; init; }

    public required ImportReport Report { get; init; }
}
