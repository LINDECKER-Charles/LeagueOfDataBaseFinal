using LoDb.Domain.Builds.Structures;
using LoDb.Domain.Catalog.Modes;
using LoDb.Domain.Derived.Items;

namespace LoDb.Domain.Builds.Rules;

/// <summary>
/// The whole check of a structure before it is saved: the rules of the validator, then the
/// items its mode's map does not offer, named so the author knows which to replace.
/// </summary>
/// <remarks>
/// An id the patch does not know is the validator's <see cref="BuildErrors.StepItemUnknown"/>,
/// a patch problem, never also a mode one.
/// </remarks>
public static class BuildCatalogGate
{
    public static StructureVerdict Evaluate(
        BuildStructureInput structure,
        GameMode mode,
        BuildCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(catalog);
        var codes = BuildStructureValidator.Validate(structure, catalog);
        var wanted = StepItemIds(structure);

        // Named in dataset order, each item once, as the legacy message listed them.
        var unavailable = ItemPlayability.UnavailableNames(
            catalog.Items.Where(item => wanted.Contains(item.Id)),
            mode);
        return new StructureVerdict
        {
            Codes = unavailable.Count == 0 ? codes : [.. codes, BuildErrors.ItemMode],
            UnavailableItems = unavailable,
        };
    }

    private static HashSet<string> StepItemIds(BuildStructureInput structure) =>
        (structure.Steps ?? [])
            .OfType<StepInput>()
            .SelectMany(static step => step.Items ?? [])
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
}
