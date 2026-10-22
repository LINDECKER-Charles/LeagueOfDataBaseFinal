using LoDb.Domain.Catalog.Runes;

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
        throw new NotImplementedException("L1");
    }
}
