using LoDb.Domain.Builds.Structures;

namespace LoDb.Domain.Builds.Rules;

/// <summary>
/// Checks a submitted structure against the patch it is pinned to: the champion exists, the
/// rune page is legal, the purchase order is bounded and names known items.
/// </summary>
/// <remarks>
/// Every section is checked, so one answer lists all the mistakes; a code appears once, in
/// the order champion, runes, steps.
/// </remarks>
public static class BuildStructureValidator
{
    public static IReadOnlyList<string> Validate(
        BuildStructureInput structure,
        BuildCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(catalog);
        IEnumerable<string> errors =
        [
            .. ChampionErrors(structure.ChampionId, catalog),
            .. RunePageRules.Errors(structure.Runes, catalog.Runes),
            .. StepRules.Errors(structure.Steps, catalog),
        ];
        return [.. errors.Distinct(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> ChampionErrors(string? championId, BuildCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(championId))
        {
            return [BuildErrors.StructureInvalid];
        }

        return catalog.HasChampion(championId.Trim()) ? [] : [BuildErrors.ChampionUnknown];
    }
}
