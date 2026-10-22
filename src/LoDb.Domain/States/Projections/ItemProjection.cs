using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Derived.Stats;
using LoDb.Domain.Editions;
using LoDb.Domain.States.Fields;
using LoDb.Domain.States.Projections.Parts;

namespace LoDb.Domain.States.Projections;

/// <summary>
/// The displayed projection of an item: name, plaintext, description, gold, recipe (its
/// components in order), stats (<c>ItemStats.Of</c>), claimable maps, tags, depth, whether it
/// is consumed, required champion and ally. Never what it builds into nor its twin: other
/// entities give them.
/// </summary>
public static class ItemProjection
{
    /// <summary>
    /// Its fields in the order of <c>ChangeField</c>, a template's subjects in the page's
    /// order; texts in <c>DdragonText.PlainText</c> form.
    /// </summary>
    public static IReadOnlyList<ProjectedField> Of(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var stats = ItemStats.Of(item.Stats);
        var maps = ItemEdition.ClaimableMapIds(item.Id, item.Maps)
            .Select(ProjectedFields.Invariant);
        return ProjectedFields.Of(
            ProjectedFields.Value(FieldKey.Of(ChangeField.Name), item.Name),
            ProjectedFields.Text(FieldKey.Of(ChangeField.Description), item.Description),
            Stats(stats, ChangeField.Stat, isPercent: false),
            ProjectedFields.Text(FieldKey.Of(ChangeField.Plaintext), item.Plaintext),
            ProjectedFields.Number(FieldKey.Of(ChangeField.GoldTotal), item.Gold.Total),
            ProjectedFields.Number(FieldKey.Of(ChangeField.GoldBase), item.Gold.Base),
            ProjectedFields.Number(FieldKey.Of(ChangeField.GoldSell), item.Gold.Sell),
            ProjectedFields.Flag(FieldKey.Of(ChangeField.Purchasable), item.Gold.IsPurchasable),
            ProjectedFields.List(FieldKey.Of(ChangeField.Recipe), item.From),
            Stats(stats, ChangeField.StatPercent, isPercent: true),
            ProjectedFields.Set(FieldKey.Of(ChangeField.Maps), maps),
            ProjectedFields.Set(FieldKey.Of(ChangeField.Tags), item.Tags),
            ProjectedFields.Number(FieldKey.Of(ChangeField.Depth), item.Depth),
            ProjectedFields.Flag(FieldKey.Of(ChangeField.Consumable), item.IsConsumed),
            ProjectedFields.Value(FieldKey.Of(ChangeField.RequiredChampion), item.RequiredChampion),
            ProjectedFields.Value(FieldKey.Of(ChangeField.RequiredAlly), item.RequiredAlly));
    }

    // A flat stat and a percent one of the same game stat stay two fields (move speed).
    private static IEnumerable<ProjectedField> Stats(
        IReadOnlyList<ItemStat> stats,
        ChangeField field,
        bool isPercent) =>
        stats
            .Where(stat => stat.IsPercent == isPercent)
            .SelectMany(stat =>
                ProjectedFields.Number(FieldKey.Of(field, StatTokens.Of(stat.Stat)), stat.Value));
}
