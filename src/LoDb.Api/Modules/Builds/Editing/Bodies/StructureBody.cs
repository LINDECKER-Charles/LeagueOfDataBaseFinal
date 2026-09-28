using LoDb.Domain.Builds.Structures;

namespace LoDb.Api.Modules.Builds.Editing.Bodies;

/// <summary>
/// The champion, rune page and purchase order of a submitted build, in the shape the build
/// stores them; the rules, not the binding, judge what is missing.
/// </summary>
internal sealed record StructureBody
{
    /// <summary>A champion id of the build's patch, such as MonkeyKing.</summary>
    public string? ChampionId { get; init; }

    /// <summary>
    /// Four picks of the primary path, one per row, the keystone first; two of the secondary
    /// path, in two rows other than the keystones'.
    /// </summary>
    public RunePageInput? Runes { get; init; }

    /// <summary>1 to 10 steps, 1 to 8 items each, 40 items in all.</summary>
    public IReadOnlyList<StepBody?>? Steps { get; init; }

    public BuildStructureInput ToInput() => new()
    {
        ChampionId = ChampionId,
        Runes = Runes,
        Steps = Steps?.Select(static step => step?.ToInput()).ToList(),
    };
}
