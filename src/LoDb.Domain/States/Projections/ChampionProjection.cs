using LoDb.Domain.Catalog.Champions;

namespace LoDb.Domain.States.Projections;

/// <summary>
/// The displayed projection of a champion: name, title, roles, resource, ratings, stat lines,
/// lore (the blurb when it has none), tips, passive, each spell by slot, and skins by id and
/// name. Never the art numbers nor the chromas (CommunityDragon), never an image.
/// </summary>
public static class ChampionProjection
{
    /// <summary>
    /// Its fields in the order of <c>ChangeField</c>, a template's subjects in the page's
    /// order; texts in <c>DdragonText.PlainText</c> form.
    /// </summary>
    public static IReadOnlyList<ProjectedField> Of(ChampionDetail champion)
    {
        ArgumentNullException.ThrowIfNull(champion);
        throw new NotImplementedException("L1");
    }
}
