using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Stats;
using LoDb.Domain.States.Fields;

namespace LoDb.Domain.States.Projections.Parts;

/// <summary>
/// A champion's ratings and stat lines as the page shows them: the lines of
/// <see cref="ChampionStatRows"/> whose base value the version ships, then their gains per
/// level.
/// </summary>
internal static class ChampionStatFields
{
    public static IEnumerable<ProjectedField> Ratings(ChampionRatings? info)
    {
        if (info is null)
        {
            return [];
        }

        int[] ratings = [info.Attack, info.Defense, info.Magic, info.Difficulty];
        return FieldTokens.Ratings.Zip(ratings).SelectMany(static rating =>
            ProjectedFields.Number(FieldKey.Of(ChangeField.Rating, rating.First), rating.Second));
    }

    public static IEnumerable<ProjectedField> Stats(IReadOnlyDictionary<string, double> stats)
    {
        var shown = ChampionStatRows.All.Where(row => stats.ContainsKey(row.BaseKey)).ToList();
        var bases = shown.SelectMany(row =>
            Number(ChangeField.Stat, row.Stat, stats[row.BaseKey]));
        var gains = shown.SelectMany(row =>
            row.PerLevelKey is { } key && stats.TryGetValue(key, out var gain)
                ? Number(ChangeField.StatPerLevel, row.Stat, gain)
                : []);
        return [.. bases, .. gains];
    }

    private static IEnumerable<ProjectedField> Number(
        ChangeField field,
        GameStat stat,
        double value) =>
        ProjectedFields.Number(FieldKey.Of(field, StatTokens.Of(stat)), value);
}
