namespace LoDb.Domain.Builds.Structures;

/// <summary>
/// Canonicalizes a structure before it is stored: trimmed texts, integer rune ids and string
/// item ids, so the database never keeps what a form happened to send.
/// </summary>
/// <remarks>
/// Runs on a structure the rules accepted; on a malformed one it still degrades to empty
/// shapes rather than failing, an unreadable id becoming <see cref="RunePage.Unset"/>.
/// </remarks>
public static class BuildStructureNormalizer
{
    public static BuildStructure Normalize(BuildStructureInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new BuildStructure
        {
            ChampionId = input.ChampionId?.Trim() ?? string.Empty,
            Runes = Normalize(input.Runes),
            Steps = [.. (input.Steps ?? []).Select(Normalize)],
        };
    }

    /// <summary>The page with every unreadable id unset; blank when there is no page.</summary>
    public static RunePage Normalize(RunePageInput? runes) =>
        runes is null
            ? RunePage.Blank
            : new RunePage
            {
                PrimaryStyleId = runes.PrimaryStyleId ?? RunePage.Unset,
                PrimarySelections = IdsOf(runes.PrimarySelections),
                SecondaryStyleId = runes.SecondaryStyleId ?? RunePage.Unset,
                SecondarySelections = IdsOf(runes.SecondarySelections),
            };

    private static int[] IdsOf(IReadOnlyList<int?>? selections) =>
        [.. (selections ?? []).Select(static id => id ?? RunePage.Unset)];

    private static BuildStep Normalize(StepInput? step)
    {
        var note = step?.Note?.Trim();
        return new BuildStep
        {
            Label = step?.Label?.Trim() ?? string.Empty,
            Note = string.IsNullOrEmpty(note) ? null : note,
            Items = [.. (step?.Items ?? []).Select(static id => id ?? string.Empty)],
        };
    }
}
