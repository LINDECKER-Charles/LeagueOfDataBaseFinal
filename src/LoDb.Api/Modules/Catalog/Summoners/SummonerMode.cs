using LoDb.Domain.Catalog.Modes;

namespace LoDb.Api.Modules.Catalog.Summoners;

/// <summary>A queue a summoner spell is allowed in, as its plaque shows it.</summary>
internal sealed record SummonerMode
{
    /// <summary>Data Dragon mode, such as CLASSIC or JADE (LoL Classic).</summary>
    public required string Code { get; init; }

    /// <summary>English display name; null for a mode shown by its edition instead.</summary>
    public string? Label { get; init; }

    /// <summary>Whether the list's mode facet offers it.</summary>
    public required bool Facetable { get; init; }

    public static IReadOnlyList<SummonerMode> Of(IEnumerable<string> modes) =>
        [.. GameModeLabels.Displayable(modes).Select(static mode => new SummonerMode
        {
            Code = mode,
            Label = GameModeLabels.LabelOf(mode),
            Facetable = GameModeLabels.IsFacetable(mode),
        })];
}
