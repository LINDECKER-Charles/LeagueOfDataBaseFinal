using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;

namespace LoDb.Api.Modules.Builds.Import;

/// <summary>
/// An unsaved build the editor opens: the fields it submits to <c>POST /api/builds</c> once
/// its author has reviewed them.
/// </summary>
internal sealed record BuildDraft
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Always false: an import is a new build its author has not published yet.</summary>
    public required bool IsPublic { get; init; }

    /// <summary>The patch the draft was projected on.</summary>
    public required string GameVersion { get; init; }

    public required GameMode GameMode { get; init; }

    public required string Language { get; init; }

    public required BuildStructure Structure { get; init; }
}
