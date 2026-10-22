using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Ranges;
using LoDb.Domain.States.Fields;

namespace LoDb.Domain.States.Projections.Parts;

/// <summary>
/// A champion's spells by slot, Q to R, field by field as the page shows them: the range with
/// its placeholders masked, the charges only when the spell stores some.
/// </summary>
internal static class ChampionSpellFields
{
    private static readonly IReadOnlyList<(ChangeField Field, Func<FieldKey, ChampionSpell,
        IEnumerable<ProjectedField>> Project)> Templates =
    [
        (ChangeField.SpellName, static (key, spell) => ProjectedFields.Value(key, spell.Name)),
        (ChangeField.SpellDescription,
            static (key, spell) => ProjectedFields.Text(key, spell.Description)),
        (ChangeField.SpellCooldown,
            static (key, spell) => ProjectedFields.Value(key, spell.CooldownBurn)),
        (ChangeField.SpellCost, static (key, spell) => ProjectedFields.Value(key, spell.CostBurn)),
        (ChangeField.SpellRange, static (key, spell) =>
            ProjectedFields.Value(key, RangeSentinels.MaskAbilityRange(spell.RangeBurn))),
        (ChangeField.SpellMaxRank,
            static (key, spell) => ProjectedFields.Number(key, spell.MaxRank)),
        (ChangeField.SpellCharges,
            static (key, spell) => ProjectedFields.Number(key, spell.Charges)),
    ];

    public static IEnumerable<ProjectedField> Of(IReadOnlyList<ChampionSpell> spells)
    {
        var slotted = spells.Zip(FieldTokens.SpellSlots).ToList();
        return [.. Templates.SelectMany(template => slotted.SelectMany(spell =>
            template.Project(FieldKey.Of(template.Field, spell.Second), spell.First)))];
    }
}
