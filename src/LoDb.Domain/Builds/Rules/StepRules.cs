using LoDb.Domain.Builds.Metadata;
using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Builds.Rules;

/// <summary>
/// The rules of the purchase order: 1 to 10 labelled steps of 1 to 8 known items each, 40
/// items at most in all, duplicates allowed.
/// </summary>
internal static class StepRules
{
    public static IEnumerable<string> Errors(IReadOnlyList<StepInput?>? steps, BuildCatalog catalog)
    {
        if (steps is null
            || steps.Count < BuildLimits.StepsMin
            || steps.Count > BuildLimits.StepsMax)
        {
            return [BuildErrors.StepsCount];
        }

        var errors = new List<string>();
        var totalItems = 0;
        foreach (var step in steps)
        {
            totalItems += StepErrors(step, catalog, errors);
        }

        if (totalItems > BuildLimits.TotalItemsMax)
        {
            errors.Add(BuildErrors.StepsTotalItems);
        }

        return errors;
    }

    // Returns what the step adds to the total: nothing once its own count is refused, so one
    // mistake never reads as two errors.
    private static int StepErrors(StepInput? step, BuildCatalog catalog, List<string> errors)
    {
        if (step is null)
        {
            errors.Add(BuildErrors.StructureInvalid);
            return 0;
        }

        errors.AddRange(TextErrors(step));
        var items = step.Items;
        if (items is null
            || items.Count < BuildLimits.ItemsPerStepMin
            || items.Count > BuildLimits.ItemsPerStepMax)
        {
            errors.Add(BuildErrors.StepItemsCount);
            return 0;
        }

        // Duplicates are legitimate (potions, stacked components): only existence matters.
        errors.AddRange(items
            .Where(id => id is null || catalog.FindItem(id) is null)
            .Select(static _ => BuildErrors.StepItemUnknown));
        return items.Count;
    }

    private static IEnumerable<string> TextErrors(StepInput step)
    {
        var label = step.Label?.Trim();
        if (string.IsNullOrEmpty(label) || !TextLength.IsWithin(label, BuildLimits.StepLabelMax))
        {
            yield return BuildErrors.StepLabel;
        }

        var note = step.Note?.Trim();
        if (step.IsNoteMalformed
            || (note is not null && !TextLength.IsWithin(note, BuildLimits.StepNoteMax)))
        {
            yield return BuildErrors.StepNote;
        }
    }
}
