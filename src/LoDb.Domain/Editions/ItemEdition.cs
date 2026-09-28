namespace LoDb.Domain.Editions;

/// <summary>
/// Edition rule for items: the classic catalogue is the six-digit id range Riot prefixed with
/// "77" (771004 is the classic twin of 1004).
/// </summary>
/// <remarks>
/// Never derived from the <c>maps</c> flags (UP 6): they are incoherent for the classic range
/// (mostly flagged on ARAM, one on the Rift, a few on 453 only) while every current item is
/// flagged on 453 too. The id prefix is the one signal Riot applies consistently, the same way
/// Arena variants carry a "22" prefix.
/// </remarks>
public static class ItemEdition
{
    /// <summary>Data Dragon map id of LoL Classic's Classic Rift.</summary>
    public const int ClassicRiftMapId = 453;

    private const string ClassicPrefix = "77";
    private const int ModernIdLength = 4;
    private const int ClassicIdLength = 6;

    public static Edition Of(string id) => IsClassic(id) ? Edition.Classic : Edition.Modern;

    /// <summary>
    /// Id the other edition's twin would carry, or <see langword="null"/> when the id cannot
    /// have one (Arena and event ranges). Whether the twin exists is the caller's question.
    /// </summary>
    public static string? CounterpartId(string id)
    {
        if (IsClassic(id))
        {
            return id[ClassicPrefix.Length..];
        }

        return IsDigits(id, ModernIdLength) ? ClassicPrefix + id : null;
    }

    /// <summary>
    /// Label that stays unambiguous in a flat text list: a classic item shares its name with
    /// the current one, so it carries its id, the one token a player can match on its page.
    /// </summary>
    public static string QualifiedName(string id, string name) =>
        IsClassic(id) ? $"{name} [{id}]" : name;

    /// <summary>
    /// Map ids an item may honestly claim, in ascending order: a classic item belongs to the
    /// Classic Rift only; a current item keeps its flags minus 453, which every current item
    /// carries without existing there.
    /// </summary>
    public static IReadOnlyList<int> ClaimableMapIds(
        string id,
        IReadOnlyDictionary<int, bool> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        if (IsClassic(id))
        {
            return [ClassicRiftMapId];
        }

        return [.. maps
            .Where(flag => flag.Value && flag.Key != ClassicRiftMapId)
            .Select(flag => flag.Key)
            .Order()];
    }

    private static bool IsClassic(string id) =>
        IsDigits(id, ClassicIdLength) && id.StartsWith(ClassicPrefix, StringComparison.Ordinal);

    private static bool IsDigits(string id, int length)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.Length == length && id.All(char.IsAsciiDigit);
    }
}
