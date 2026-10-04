using System.Text;

namespace LoDb.Api.Modules.Audit.Reading.Query;

/// <summary>Turns a text into a LIKE pattern that matches it alone.</summary>
internal static class LikeEscape
{
    /// <summary>The escape character the pattern is written with.</summary>
    public const string Character = "\\";

    private const char EscapeChar = '\\';

    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var pattern = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character is EscapeChar or '%' or '_')
            {
                pattern.Append(EscapeChar);
            }

            pattern.Append(character);
        }

        return pattern.ToString();
    }
}
