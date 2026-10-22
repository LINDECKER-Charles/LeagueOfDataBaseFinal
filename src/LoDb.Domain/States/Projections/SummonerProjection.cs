using LoDb.Domain.Catalog.Summoners;

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
        throw new NotImplementedException("L1");
    }
}
