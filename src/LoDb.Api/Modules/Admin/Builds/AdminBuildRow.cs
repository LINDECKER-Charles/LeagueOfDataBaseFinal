using LoDb.Api.Modules.Admin.Http;

namespace LoDb.Api.Modules.Admin.Builds;

/// <summary>A build in the moderation list.</summary>
internal sealed record AdminBuildRow
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string ChampionId { get; init; }

    public required string GameVersion { get; init; }

    /// <summary>sr, aram, nexus_blitz or arena.</summary>
    public required string GameMode { get; init; }

    public required string Language { get; init; }

    public required bool IsPublic { get; init; }

    /// <summary>Up votes minus down votes.</summary>
    public required int Score { get; init; }

    public required AdminUserRef Owner { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
