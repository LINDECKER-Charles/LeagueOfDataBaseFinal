namespace LoDb.Domain.Editions;

/// <summary>
/// Edition rule for summoner spells: a classic spell is played in Data Dragon's JADE mode and
/// carries a "_Jade" suffixed id (SummonerFlash_Jade is the classic twin of SummonerFlash).
/// </summary>
/// <remarks>
/// Either signal makes a spell classic (UP 6). The mode list is the authoritative one, but
/// callers that only hold an id (rankings, links) must reach the same answer, and on every
/// shipped version the suffix and the mode go together.
/// </remarks>
public static class SummonerSpellEdition
{
    /// <summary>Data Dragon game mode of LoL Classic.</summary>
    public const string ClassicMode = "JADE";

    private const string ClassicSuffix = "_Jade";

    public static Edition Of(string id, IEnumerable<string> modes)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(modes);
        return modes.Contains(ClassicMode, StringComparer.Ordinal)
            || id.EndsWith(ClassicSuffix, StringComparison.Ordinal)
                ? Edition.Classic
                : Edition.Modern;
    }

    /// <summary>
    /// Id the other edition's twin would carry. Whether the twin exists is the caller's
    /// question.
    /// </summary>
    public static string CounterpartId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EndsWith(ClassicSuffix, StringComparison.Ordinal)
            ? id[..^ClassicSuffix.Length]
            : id + ClassicSuffix;
    }
}
