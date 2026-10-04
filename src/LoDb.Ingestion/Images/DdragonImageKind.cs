namespace LoDb.Ingestion.Images;

/// <summary>
/// What a Data Dragon image depicts: it decides the CDN folder and the manifest type.
/// </summary>
public enum DdragonImageKind
{
    /// <summary>Square champion portrait, <c>{version}/img/champion/</c>.</summary>
    Champion,

    /// <summary>Champion passive, <c>{version}/img/passive/</c>.</summary>
    Passive,

    /// <summary>Champion ability, <c>{version}/img/spell/</c>.</summary>
    ChampionSpell,

    /// <summary>Item icon, <c>{version}/img/item/</c>.</summary>
    Item,

    /// <summary>
    /// Summoner spell, <c>{version}/img/spell/</c> like the abilities but recorded under its
    /// own manifest type.
    /// </summary>
    SummonerSpell,

    /// <summary>Rune path or rune icon, under the unversioned <c>img/</c> root (UP 5).</summary>
    Rune,
}
