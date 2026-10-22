using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Derived.Ranges;
using LoDb.Domain.States.Fields;
using LoDb.Domain.States.Projections.Parts;

namespace LoDb.Domain.States.Projections;

/// <summary>
/// The displayed projection of a summoner spell: name, description, cooldown, cost and its
/// type, range (the whole map as <c>global</c>), summoner level, charges and modes. Never its
/// key nor its twin.
/// </summary>
public static class SummonerProjection
{
    /// <summary>
    /// Its fields in the order of <c>ChangeField</c>, a template's subjects in the page's
    /// order; texts in <c>DdragonText.PlainText</c> form.
    /// </summary>
    public static IReadOnlyList<ProjectedField> Of(SummonerSpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return ProjectedFields.Of(
            ProjectedFields.Value(FieldKey.Of(ChangeField.Name), spell.Name),
            ProjectedFields.Text(FieldKey.Of(ChangeField.Description), spell.Description),
            Numbers(ChangeField.Cooldown, spell.Cooldown),
            Numbers(ChangeField.Cost, spell.Cost),
            ProjectedFields.Value(FieldKey.Of(ChangeField.CostType), spell.CostType),
            ProjectedFields.List(FieldKey.Of(ChangeField.Range), spell.Range.Select(RangeOf)),
            ProjectedFields.Number(FieldKey.Of(ChangeField.SummonerLevel), spell.SummonerLevel),
            ProjectedFields.Number(FieldKey.Of(ChangeField.Charges), spell.Charges),
            ProjectedFields.Set(FieldKey.Of(ChangeField.Modes), spell.Modes));
    }

    private static IEnumerable<ProjectedField> Numbers(
        ChangeField field,
        IEnumerable<double> values) =>
        ProjectedFields.List(FieldKey.Of(field), values.Select(ProjectedFields.Invariant));

    // The page shows a range that reaches the whole map as global, not as its sentinel.
    private static string RangeOf(double range) =>
        RangeSentinels.IsGlobal(range) ? FieldTokens.GlobalRange : ProjectedFields.Invariant(range);
}
