using LoDb.Parity.Deviations;

namespace LoDb.Parity.Classification;

/// <summary>Gives each deviation the first matching rule: rules go from narrow to broad.</summary>
public static class Classifier
{
    public static IReadOnlyList<ClassifiedDeviation> Classify(
        IEnumerable<Deviation> deviations,
        IReadOnlyList<DeviationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(deviations);
        ArgumentNullException.ThrowIfNull(rules);
        return
        [
            .. deviations.Select(deviation => new ClassifiedDeviation(
                deviation,
                rules.FirstOrDefault(rule => rule.Matches(deviation)))),
        ];
    }
}
