using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>
/// The Data Dragon image each entry shows, or <see langword="null"/> when its field names
/// none the manifest can hold, the rule the ingestion skips such images by.
/// </summary>
internal static class EntityImages
{
    public static DdragonImage? Portrait(ChampionDetail champion)
    {
        ArgumentNullException.ThrowIfNull(champion);
        return Of(DdragonImageKind.Champion, champion.Summary.Image);
    }

    public static DdragonImage? Passive(ChampionPassive? passive) =>
        Of(DdragonImageKind.Passive, passive?.Image);

    public static DdragonImage? Ability(ChampionSpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return Of(DdragonImageKind.ChampionSpell, spell.Image);
    }

    public static DdragonImage? Icon(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ItemIcon(item.Image);
    }

    /// <summary>An item icon by file name, as a recipe node carries it.</summary>
    public static DdragonImage? ItemIcon(string? file) => Of(DdragonImageKind.Item, file);

    public static DdragonImage? Icon(RuneTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return Of(DdragonImageKind.Rune, tree.Icon);
    }

    public static DdragonImage? Icon(Rune rune)
    {
        ArgumentNullException.ThrowIfNull(rune);
        return Of(DdragonImageKind.Rune, rune.Icon);
    }

    public static DdragonImage? Icon(SummonerSpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return Of(DdragonImageKind.SummonerSpell, spell.Image);
    }

    private static DdragonImage? Of(DdragonImageKind kind, string? file) =>
        !string.IsNullOrWhiteSpace(file) && file.Length <= DdragonImage.MaxFileLength
            ? new DdragonImage(kind, file)
            : null;
}
