using LoDb.Domain.Catalog.Items;

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
        throw new NotImplementedException("L1");
    }
}
