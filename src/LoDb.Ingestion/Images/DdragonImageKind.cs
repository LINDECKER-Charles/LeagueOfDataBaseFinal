namespace LoDb.Ingestion.Images;

/// <summary>
/// What a Data Dragon image depicts: it decides the CDN folder and the manifest type, and
/// whether it is stored as fetched or as WebP variants of a given width.
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

    /// <summary>
    /// A WebP variant of a champion's centered splash art, <c>img/champion/centered/</c>:
    /// the hero of the champion page (<see cref="Art.ArtVariants"/>).
    /// </summary>
    CenteredArt,

    /// <summary>
    /// A WebP variant of a champion's loading-screen portrait, <c>img/champion/loading/</c>:
    /// the art of the list's card (<see cref="Art.ArtVariants"/>).
    /// </summary>
    LoadingArt,
}
