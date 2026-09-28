using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Derived.Champions;
using LoDb.Ingestion.Ddragon.Raw.Champions;

namespace LoDb.Ingestion.Normalization.Mapping;

/// <summary>
/// A champion's Data Dragon skins, numbered, with their CommunityDragon chromas and without
/// the chromas Data Dragon lists as skins (UP 3, UP 9).
/// </summary>
internal static class SkinMapper
{
    public static IReadOnlyList<Skin> Map(List<RawChampionSkin?>? skins, ChromaCatalog chromas)
    {
        if (skins is null)
        {
            return [];
        }

        var mapped = new List<Skin>(skins.Count);
        for (var index = 0; index < skins.Count; index++)
        {
            if (skins[index] is { } skin)
            {
                mapped.Add(Map(skin, index, chromas));
            }
        }

        return ChromaSkins.Without(mapped);
    }

    // The position in the upstream list stands in for a missing num, null entries included:
    // it is the art index. 0.x writes no skin id, so such a skin has no chroma.
    private static Skin Map(RawChampionSkin skin, int index, ChromaCatalog chromas)
    {
        var id = RawValues.Text(skin.Id);
        return new Skin
        {
            Id = id,
            Number = SkinNumber.Of(skin.Num, index),
            Name = RawValues.Text(skin.Name),
            Chromas = chromas.For(id),
        };
    }
}
