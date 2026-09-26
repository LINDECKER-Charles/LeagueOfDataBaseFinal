using System.Collections.Frozen;
using System.Globalization;
using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Ddragon;

namespace LoDb.Ingestion.Catalog.Hotlinks;

/// <summary>
/// The champion media hotlinked rather than ingested (UP 8): the art of every skin, the
/// chroma swatches and the ability videos.
/// </summary>
/// <remarks>
/// The art comes from Data Dragon, under Riot's internal spelling where it differs from the
/// public id (<c>FiddleSticks</c>, UP 7); the swatches from CommunityDragon (UP 9); the videos
/// from the CloudFront CDN behind Riot's official champion pages, keyed by the numeric
/// champion key on four digits. The pages' content security policy must allow these three
/// hosts.
/// </remarks>
public static class ChampionHotlinks
{
    private const string KeyFormat = "D4";

    private static readonly FrozenDictionary<AbilitySlot, char> SlotLetters =
        new Dictionary<AbilitySlot, char>
        {
            [AbilitySlot.Passive] = 'P',
            [AbilitySlot.Q] = 'Q',
            [AbilitySlot.W] = 'W',
            [AbilitySlot.E] = 'E',
            [AbilitySlot.R] = 'R',
        }.ToFrozenDictionary();

    /// <summary>Root of the ability videos.</summary>
    public static Uri AbilityVideoRoot { get; } =
        new("https://d28xe8vt774jo5.cloudfront.net/champion-abilities/");

    /// <summary>Art of a skin ("…/cdn/img/champion/splash/FiddleSticks_0.jpg").</summary>
    /// <param name="championId">Public champion id ("Fiddlesticks").</param>
    /// <param name="kind">Art family.</param>
    /// <param name="skinNumber"><see cref="Skin.Number"/>, 0 for the base skin.</param>
    public static Uri Art(string championId, ChampionArtKind kind, int skinNumber) =>
        DdragonUrls.Image(DdragonImagePath.ChampionArt(championId, kind, skinNumber));

    /// <summary>The swatch of a chroma, its only art.</summary>
    public static Uri ChromaSwatch(Chroma chroma)
    {
        ArgumentNullException.ThrowIfNull(chroma);
        return CommunityDragonUrls.Asset(chroma.Image);
    }

    /// <summary>
    /// The preview of an ability, or <see langword="null"/> when the champion's key is not a
    /// number and names no video.
    /// </summary>
    public static AbilityVideo? Video(ChampionSummary champion, AbilitySlot slot)
    {
        ArgumentNullException.ThrowIfNull(champion);
        var style = NumberStyles.None;
        if (!int.TryParse(champion.Key, style, CultureInfo.InvariantCulture, out var key))
        {
            return null;
        }

        var padded = key.ToString(KeyFormat, CultureInfo.InvariantCulture);
        var stem = $"{padded}/ability_{padded}_{SlotLetters[slot]}1";
        return new AbilityVideo
        {
            Webm = new Uri(AbilityVideoRoot, $"{stem}.webm"),
            Mp4 = new Uri(AbilityVideoRoot, $"{stem}.mp4"),
            Poster = new Uri(AbilityVideoRoot, $"{stem}.jpg"),
        };
    }
}
