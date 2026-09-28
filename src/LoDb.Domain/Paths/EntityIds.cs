using System.Globalization;

namespace LoDb.Domain.Paths;

/// <summary>
/// Shapes an entity id may take inside a path: ASCII digits for items and rune paths, an
/// ASCII word for champions and summoner spells.
/// </summary>
/// <remarks>
/// Checked at the source so that no id can carry a separator, a dot segment or a character
/// that would need escaping into a URL.
/// </remarks>
internal static class EntityIds
{
    private const char WordJoiner = '_';

    internal static bool IsNumber(string id) => id.Length > 0 && id.All(char.IsAsciiDigit);

    internal static bool IsWord(string id) => id.Length > 0 && id.All(IsWordCharacter);

    internal static string RequireNumber(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return IsNumber(id) ? id : throw Invalid(id);
    }

    internal static string RequireNumber(int id) =>
        RequireNumber(id.ToString(CultureInfo.InvariantCulture));

    internal static string RequireWord(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return IsWord(id) ? id : throw Invalid(id);
    }

    private static bool IsWordCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character == WordJoiner;

    private static ArgumentException Invalid(string id) =>
        new($"'{id}' cannot be the id of an entity path.", nameof(id));
}
