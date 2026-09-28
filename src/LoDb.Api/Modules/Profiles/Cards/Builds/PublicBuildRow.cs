namespace LoDb.Api.Modules.Profiles.Cards.Builds;

/// <summary>A public build as <see cref="IPublicBuildSource"/> hands it over.</summary>
internal sealed record PublicBuildRow
{
    /// <summary>The token of its <c>/b/{token}</c> link.</summary>
    public required string ShareToken { get; init; }

    public required string Name { get; init; }

    public required string ChampionId { get; init; }

    /// <summary>The patch the build is pinned to.</summary>
    public required string GameVersion { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
