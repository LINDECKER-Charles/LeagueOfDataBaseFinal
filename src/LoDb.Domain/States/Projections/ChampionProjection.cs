using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.States.Fields;
using LoDb.Domain.States.Projections.Parts;
using LoDb.Domain.Text;

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
        var summary = champion.Summary;
        return ProjectedFields.Of(
            ProjectedFields.Value(FieldKey.Of(ChangeField.Name), summary.Name),
            ProjectedFields.Value(FieldKey.Of(ChangeField.Title), summary.Title),
            ProjectedFields.Value(FieldKey.Of(ChangeField.Lore), LoreOf(champion)),
            ProjectedFields.List(FieldKey.Of(ChangeField.Roles), summary.Tags),
            ProjectedFields.Value(FieldKey.Of(ChangeField.Resource), summary.Partype),
            ChampionStatFields.Ratings(summary.Info),
            ChampionStatFields.Stats(summary.Stats),
            ProjectedFields.Lines(FieldKey.Of(ChangeField.AllyTips), champion.AllyTips),
            ProjectedFields.Lines(FieldKey.Of(ChangeField.EnemyTips), champion.EnemyTips),
            ProjectedFields.Value(FieldKey.Of(ChangeField.PassiveName), champion.Passive?.Name),
            ProjectedFields.Text(
                FieldKey.Of(ChangeField.PassiveDescription),
                champion.Passive?.Description),
            ChampionSpellFields.Of(champion.Spells),
            ChampionSkinFields.Of(champion.Skins));
    }

    // The page shows the blurb when the lore is missing or empty.
    private static string LoreOf(ChampionDetail champion) =>
        DdragonText.PlainText(champion.Lore) is { Length: > 0 } lore
            ? lore
            : DdragonText.PlainText(champion.Summary.Blurb);
}
