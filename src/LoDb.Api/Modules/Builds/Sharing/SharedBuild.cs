using LoDb.Api.Modules.Builds.Rendering;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Api.Modules.Builds.Views;
using LoDb.Domain.Catalog.Modes;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Builds.Sharing;

/// <summary>
/// A build as its <c>/b/{token}</c> page shows it, rendered on its own patch: what its
/// author saw, ghosts included.
/// </summary>
internal sealed record SharedBuild
{
    public required int Id { get; init; }

    public required string ShareToken { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>A private build is unlisted: its link works, but nobody may vote on it.</summary>
    public required bool IsPublic { get; init; }

    /// <summary>The patch the build is pinned to, and rendered on.</summary>
    public required string GameVersion { get; init; }

    public required GameMode GameMode { get; init; }

    /// <summary>The Data Dragon language the build is written in.</summary>
    public required string Language { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// The version the visitor browses, the latest unless they name one; null when Data
    /// Dragon could not tell it.
    /// </summary>
    public string? CurrentVersion { get; init; }

    /// <summary>Whether the build's patch is not the one browsed: the page says so.</summary>
    public required bool PatchMismatch { get; init; }

    public required OwnerView Owner { get; init; }

    public required ChampionView Champion { get; init; }

    public required RunePageView Runes { get; init; }

    public required IReadOnlyList<StepView> Steps { get; init; }

    /// <summary>The cost of the whole purchase order, ghosts left out.</summary>
    public required int TotalGold { get; init; }

    /// <summary>The votes of a public build; null for a private one.</summary>
    public VoteState? Vote { get; init; }

    /// <param name="build">The build, its owner loaded.</param>
    /// <param name="scene">The build's patch, or ghosts only when it could not be read.</param>
    /// <param name="currentVersion">The version browsed; null when unknown.</param>
    public static SharedBuild Of(Build build, BuildScene scene, string? currentVersion)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(scene);
        var owner = build.Owner ?? throw new ArgumentException("The owner is not loaded.");
        var structure = StoredStructures.Normalized(build);
        return new SharedBuild
        {
            Id = build.Id,
            ShareToken = build.ShareToken,
            Name = build.Name,
            Description = build.Description,
            IsPublic = build.IsPublic,
            GameVersion = build.GameVersion,
            GameMode = StoredMode.Of(build),
            Language = build.Language,
            CreatedAt = build.CreatedAt,
            UpdatedAt = build.UpdatedAt,
            CurrentVersion = currentVersion,
            PatchMismatch = currentVersion is not null
                && !string.Equals(build.GameVersion, currentVersion, StringComparison.Ordinal),
            Owner = OwnerView.Of(owner),
            Champion = scene.Champion(structure.ChampionId),
            Runes = scene.Runes(structure.Runes),
            Steps = scene.Steps(structure.Steps),
            TotalGold = scene.TotalGold(structure.Steps),
        };
    }
}
