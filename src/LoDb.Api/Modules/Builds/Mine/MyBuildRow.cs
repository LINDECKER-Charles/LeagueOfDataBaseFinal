using LoDb.Api.Modules.Builds.Rendering;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Api.Modules.Builds.Views;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Builds.Mine;

/// <summary>A build in its owner's list, its champion and keystone on the patch browsed.</summary>
internal sealed record MyBuildRow
{
    public required int Id { get; init; }

    /// <summary>The token of its <c>/b/{token}</c> link.</summary>
    public required string ShareToken { get; init; }

    public required string Name { get; init; }

    /// <summary>The patch the build is pinned to.</summary>
    public required string GameVersion { get; init; }

    public required GameMode GameMode { get; init; }

    /// <summary>The Data Dragon language the build is written in.</summary>
    public required string Language { get; init; }

    public required bool IsPublic { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required ChampionView Champion { get; init; }

    public required PerkView Keystone { get; init; }

    public static MyBuildRow Of(Build build, BuildStructure structure, BuildScene scene)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(scene);
        return new MyBuildRow
        {
            Id = build.Id,
            ShareToken = build.ShareToken,
            Name = build.Name,
            GameVersion = build.GameVersion,
            GameMode = StoredMode.Of(build),
            Language = build.Language,
            IsPublic = build.IsPublic,
            CreatedAt = build.CreatedAt,
            UpdatedAt = build.UpdatedAt,
            Champion = scene.Champion(structure.ChampionId),
            Keystone = scene.Keystone(structure.Runes),
        };
    }
}
