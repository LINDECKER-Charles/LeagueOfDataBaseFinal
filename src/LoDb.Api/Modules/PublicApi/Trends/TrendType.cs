using LoDb.Domain.Editions;

namespace LoDb.Api.Modules.PublicApi.Trends;

/// <summary>
/// A type of <c>/v1/trends</c>: the segment of its path, and the type its views are
/// counted under in the daily aggregates (<c>{type}:{id}</c>).
/// </summary>
internal sealed class TrendType
{
    private const string ModernEdition = "modern";
    private const string ClassicEdition = "classic";

    private TrendType(string segment, string entityType)
    {
        Segment = segment;
        EntityType = entityType;
    }

    public static TrendType Champions { get; } = new("champions", "champion");

    public static TrendType Items { get; } = new("items", "item");

    /// <summary>The site counts the rune pages under the name of their dataset.</summary>
    public static TrendType Runes { get; } = new("runes", "runesReforged");

    public static TrendType Summoners { get; } = new("summoners", "summoner");

    /// <summary>Every type, in the order of their segments.</summary>
    public static IReadOnlyList<TrendType> All { get; } = [Champions, Items, Runes, Summoners];

    /// <summary>The refusal of a segment no type has, which lists them all.</summary>
    public static string UnknownMessage { get; } =
        "type must be one of " + string.Join(", ", All.Select(static type => type.Segment));

    public string Segment { get; }

    public string EntityType { get; }

    /// <summary>The type of a segment, spelled exactly; null when none is.</summary>
    public static TrendType? Find(string segment) =>
        All.FirstOrDefault(type => string.Equals(type.Segment, segment, StringComparison.Ordinal));

    /// <summary>
    /// <c>modern</c> or <c>classic</c> for the items and the summoner spells, whose classic
    /// twins share their names; null for the others.
    /// </summary>
    public string? EditionOf(string id)
    {
        Edition? edition = this == Items ? ItemEdition.Of(id)
            : this == Summoners ? SummonerSpellEdition.Of(id, [])
            : null;
        return edition switch
        {
            Edition.Modern => ModernEdition,
            Edition.Classic => ClassicEdition,
            _ => null,
        };
    }
}
