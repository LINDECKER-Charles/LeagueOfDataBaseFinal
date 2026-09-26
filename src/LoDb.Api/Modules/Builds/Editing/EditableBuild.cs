using LoDb.Api.Modules.Builds.Storage;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Builds.Editing;

/// <summary>
/// A build as its owner edits it: what the editor fills its form with, pinned to the patch
/// and the mode it was saved for.
/// </summary>
internal sealed record EditableBuild
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required bool IsPublic { get; init; }

    /// <summary>The patch the build is pinned to; the editor loads its catalog.</summary>
    public required string GameVersion { get; init; }

    public required GameMode GameMode { get; init; }

    /// <summary>The Data Dragon language the build is written in.</summary>
    public required string Language { get; init; }

    /// <summary>The token of its <c>/b/{token}</c> link.</summary>
    public required string ShareToken { get; init; }

    /// <summary>
    /// The champion, rune page and purchase order as stored, ghosts included: an id the patch
    /// lacks stays until its owner replaces it.
    /// </summary>
    public required BuildStructure Structure { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public static EditableBuild Of(Build build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return new EditableBuild
        {
            Id = build.Id,
            Name = build.Name,
            Description = build.Description,
            IsPublic = build.IsPublic,
            GameVersion = build.GameVersion,
            GameMode = StoredMode.Of(build),
            Language = build.Language,
            ShareToken = build.ShareToken,
            Structure = StoredStructures.Normalized(build),
            CreatedAt = build.CreatedAt,
            UpdatedAt = build.UpdatedAt,
        };
    }
}
