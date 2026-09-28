using System.Text;

namespace LoDb.Api.Modules.Admin.Http;

/// <summary>
/// The LIKE pattern of an admin search box: the text anywhere in the column, its wildcards
/// taken literally.
/// </summary>
internal static class SearchPattern
{
    /// <summary>The escape character the pattern is written with.</summary>
    public const string Escape = "\\";

    private const char EscapeChar = '\\';
    private const char Any = '%';

    /// <summary>The trimmed search, or null when blank: no filter then.</summary>
    public static string? Normalize(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    public static string Containing(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var pattern = new StringBuilder(text.Length + 2).Append(Any);
        foreach (var character in text)
        {
            if (character is EscapeChar or Any or '_')
            {
                pattern.Append(EscapeChar);
            }

            pattern.Append(character);
        }

        return pattern.Append(Any).ToString();
    }
}
