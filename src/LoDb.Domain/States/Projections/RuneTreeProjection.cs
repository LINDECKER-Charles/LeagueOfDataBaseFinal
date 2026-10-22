using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Derived.Runes;
using LoDb.Domain.States.Fields;
using LoDb.Domain.States.Projections.Parts;

namespace LoDb.Domain.States.Projections;

/// <summary>
/// The displayed projection of a rune path: name, the runes of each row, and each rune's
/// name, short and long descriptions. Never an icon.
/// </summary>
public static class RuneTreeProjection
{
    /// <summary>
    /// Its fields in the order of <c>ChangeField</c>, a template's subjects in the page's
    /// order; texts in <c>DdragonText.PlainText</c> form.
    /// </summary>
    public static IReadOnlyList<ProjectedField> Of(RuneTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var rows = tree.Slots.Select(static (slot, index) => ProjectedFields.List(
            FieldKey.Of(ChangeField.RuneSlot, RuneSlotToken.Of(index)),
            slot.Runes.Select(static rune => ProjectedFields.Invariant(rune.Id))));

        // A rune listed twice is shown once, from its first row.
        var runes = tree.Slots
            .SelectMany(static slot => slot.Runes)
            .DistinctBy(static rune => rune.Id)
            .ToList();
        return ProjectedFields.Of(
            ProjectedFields.Value(FieldKey.Of(ChangeField.Name), tree.Name),
            rows.SelectMany(static row => row),
            runes.SelectMany(static rune =>
                ProjectedFields.Value(Key(ChangeField.RuneName, rune), rune.Name)),
            runes.SelectMany(static rune =>
                ProjectedFields.Text(Key(ChangeField.RuneShortDesc, rune), rune.ShortDesc)),
            runes.SelectMany(static rune =>
                ProjectedFields.Text(Key(ChangeField.RuneLongDesc, rune), rune.LongDesc)));
    }

    private static FieldKey Key(ChangeField field, Rune rune) =>
        FieldKey.Of(field, ProjectedFields.Invariant(rune.Id));
}
