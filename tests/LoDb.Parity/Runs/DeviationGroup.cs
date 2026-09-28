using System.Text.RegularExpressions;
using LoDb.Parity.Classification;
using LoDb.Parity.Deviations;

namespace LoDb.Parity.Runs;

/// <summary>
/// Deviations of one rule, or unclassified ones of one shape (resource, kind, field with its
/// ids blanked): what the report lists, with a few examples.
/// </summary>
public sealed partial record DeviationGroup
{
    public required string Name { get; init; }

    public DeviationClass? Class { get; init; }

    public required IReadOnlyList<Deviation> Deviations { get; init; }

    /// <summary>The shape of a deviation: <c>items Value stats[].value</c>.</summary>
    public static string ShapeOf(Deviation deviation)
    {
        ArgumentNullException.ThrowIfNull(deviation);
        var field = Index().Replace(deviation.Field, "[]");
        return $"{deviation.Site.Resource} {deviation.Kind} {field}".TrimEnd();
    }

    /// <summary>Deviations grouped by rule, unclassified ones by shape.</summary>
    public static IReadOnlyList<DeviationGroup> Of(IEnumerable<ClassifiedDeviation> classified) =>
    [
        .. classified
            .GroupBy(static c => c.Rule?.Id ?? $"unclassified: {ShapeOf(c.Deviation)}")
            .Select(static group => new DeviationGroup
            {
                Name = group.Key,
                Class = group.First().Rule?.Class,
                Deviations = [.. group.Select(static c => c.Deviation)],
            })
            .OrderBy(static group => group.Class is null ? 0 : 1)
            .ThenByDescending(static group => group.Deviations.Count),
    ];

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex Index();
}
