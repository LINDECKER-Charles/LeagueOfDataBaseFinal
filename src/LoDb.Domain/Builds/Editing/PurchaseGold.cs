using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Builds.Editing;

/// <summary>
/// The gold a purchase order costs on a patch; an item the patch lacks, a ghost, counts for
/// nothing rather than hiding the total.
/// </summary>
public static class PurchaseGold
{
    /// <param name="step">The step to price.</param>
    /// <param name="goldOf">The total cost of an item id; null for an item the patch lacks.</param>
    public static int OfStep(BuildStep step, Func<string, int?> goldOf)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(goldOf);
        return step.Items.Sum(id => goldOf(id) ?? 0);
    }

    /// <param name="steps">The purchase order to price.</param>
    /// <param name="goldOf">The total cost of an item id; null for an item the patch lacks.</param>
    public static int OfBuild(IEnumerable<BuildStep> steps, Func<string, int?> goldOf)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return steps.Sum(step => OfStep(step, goldOf));
    }
}
