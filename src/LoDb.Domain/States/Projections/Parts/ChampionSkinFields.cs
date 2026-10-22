using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Paths;
using LoDb.Domain.States.Fields;

namespace LoDb.Domain.States.Projections.Parts;

/// <summary>
/// A champion's skins by id and name: never their art number nor their chromas, which come
/// from CommunityDragon and change with it, not with the patch.
/// </summary>
internal static class ChampionSkinFields
{
    public static IEnumerable<ProjectedField> Of(IReadOnlyList<Skin> skins)
    {
        // An id that cannot key a field is not one of Riot's; the first entry of an id wins.
        var keyed = skins
            .Where(static skin => EntityIds.IsWord(skin.Id))
            .DistinctBy(static skin => skin.Id, StringComparer.Ordinal)
            .ToList();
        var set = ProjectedFields.Set(
            FieldKey.Of(ChangeField.Skins),
            keyed.Select(static skin => skin.Id));
        var names = keyed.SelectMany(static skin =>
            ProjectedFields.Value(FieldKey.Of(ChangeField.SkinName, skin.Id), skin.Name));
        return [.. set, .. names];
    }
}
