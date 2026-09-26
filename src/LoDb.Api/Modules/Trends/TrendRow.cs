using LoDb.Api.Modules.Builds.Rendering;
using LoDb.Api.Modules.Builds.Storage;
using LoDb.Api.Modules.Builds.Views;
using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;

namespace LoDb.Api.Modules.Trends;

/// <summary>A build of the trends: its author, score, champion, keystone and first items.</summary>
internal sealed record TrendRow
{
    /// <summary>The items a row shows, the first of the purchase order.</summary>
    public const int ExcerptSize = 6;

    public required int Id { get; init; }

    /// <summary>The token of its <c>/b/{token}</c> link.</summary>
    public required string ShareToken { get; init; }

    public required string Name { get; init; }

    /// <summary>The patch the build is pinned to.</summary>
    public required string GameVersion { get; init; }

    public required GameMode GameMode { get; init; }

    public required string Language { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Up votes minus down votes.</summary>
    public required int Score { get; init; }

    /// <summary>1 or -1 for the caller's vote; 0 without one, or for a visitor.</summary>
    public int MyVote { get; init; }

    public required OwnerView Owner { get; init; }

    public required ChampionView Champion { get; init; }

    public required PerkView Keystone { get; init; }

    /// <summary>The first items of the purchase order, ghosts included.</summary>
    public required IReadOnlyList<ItemView> Items { get; init; }

    /// <summary>
    /// The structure cut down to what a row shows, so the images of the other items are never
    /// resolved.
    /// </summary>
    public static BuildStructure Excerpt(BuildStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        var items = structure.Steps.SelectMany(static step => step.Items).Take(ExcerptSize);
        var step = new BuildStep { Label = string.Empty, Items = [.. items] };
        return structure with { Steps = [step] };
    }

    /// <param name="ranked">The build, its author and score.</param>
    /// <param name="excerpt">Its structure, cut by <see cref="Excerpt"/>.</param>
    /// <param name="scene">The version browsed.</param>
    public static TrendRow Of(RankedBuild ranked, BuildStructure excerpt, BuildScene scene)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        ArgumentNullException.ThrowIfNull(excerpt);
        ArgumentNullException.ThrowIfNull(scene);
        var build = ranked.Build;
        return new TrendRow
        {
            Id = build.Id,
            ShareToken = build.ShareToken,
            Name = build.Name,
            GameVersion = build.GameVersion,
            GameMode = StoredMode.Of(build),
            Language = build.Language,
            CreatedAt = build.CreatedAt,
            Score = ranked.Score,
            Owner = OwnerView.Of(ranked.Owner),
            Champion = scene.Champion(excerpt.ChampionId),
            Keystone = scene.Keystone(excerpt.Runes),
            Items = [.. excerpt.Steps.SelectMany(static step => step.Items).Select(scene.Item)],
        };
    }
}
