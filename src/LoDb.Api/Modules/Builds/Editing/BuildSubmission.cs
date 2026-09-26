using System.Security.Cryptography;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Domain.Builds.Metadata;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;
using LoDb.Infrastructure.Persistence.Builds;

namespace LoDb.Api.Modules.Builds.Editing;

/// <summary>
/// A submission every rule accepted, its texts trimmed and its structure canonical: what a
/// save writes.
/// </summary>
internal sealed record BuildSubmission
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required bool IsPublic { get; init; }

    /// <summary>The patch the structure was checked on, which the build stays pinned to.</summary>
    public required string GameVersion { get; init; }

    /// <summary>The mode the structure was checked for.</summary>
    public required GameMode GameMode { get; init; }

    public required string Language { get; init; }

    public required BuildStructure Structure { get; init; }

    /// <summary>A new build of <paramref name="ownerId"/>, with a share token of its own.</summary>
    public Build Create(int ownerId, DateTimeOffset now)
    {
        var build = new Build
        {
            Name = Name,
            ChampionId = Structure.ChampionId,
            GameVersion = GameVersion,
            Runes = string.Empty,
            Steps = string.Empty,
            ShareToken = ShareTokens.Format(RandomNumberGenerator.GetBytes(ShareTokens.ByteCount)),
            GameMode = GameModes.Code(GameMode),
            Language = Language,
            OwnerId = ownerId,
            CreatedAt = now,
        };
        ApplyTo(build, now);
        return build;
    }

    /// <summary>
    /// Replaces what the owner edits; the owner, the token and the creation date stay.
    /// </summary>
    public void ApplyTo(Build build, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(build);
        build.Name = Name;
        build.Description = Description;
        build.IsPublic = IsPublic;
        build.GameVersion = GameVersion;
        build.GameMode = GameModes.Code(GameMode);
        build.Language = Language;
        build.UpdatedAt = now;
        StoredStructures.Write(build, Structure);
    }
}
