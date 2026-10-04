using System.Buffers;
using System.Text;

namespace LoDb.Domain.Text;

/// <summary>
/// Lowercasing and tokenizing restricted to ASCII, for codes, tokens and slugs.
/// </summary>
/// <remarks>
/// Culture-aware lowercasing would turn "I" into a dotless "ı" under a Turkish culture and
/// touch non-Latin scripts; identifiers built here must stay plain ASCII in every culture.
/// </remarks>
internal static class AsciiCase
{
    private const int LowerCaseOffset = 'a' - 'A';
    private const char Hyphen = '-';

    internal static char Lower(char character) =>
        char.IsAsciiLetterUpper(character) ? (char)(character + LowerCaseOffset) : character;

    internal static string Lower(string text) =>
        string.Create(text.Length, text, static (buffer, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                buffer[index] = Lower(source[index]);
            }
        });

    /// <summary>
    /// Lowercased ASCII letters and digits, every run of other characters turned into one
    /// hyphen, none at either end. Skipped characters vanish without separating words.
    /// </summary>
    internal static string Hyphenate(string text, SearchValues<char> skipped)
    {
        var token = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            var lower = Lower(character);
            if (char.IsAsciiLetterLower(lower) || char.IsAsciiDigit(lower))
            {
                token.Append(lower);
            }
            else if (!skipped.Contains(character) && token.Length > 0 && token[^1] != Hyphen)
            {
                token.Append(Hyphen);
            }
        }

        return token.ToString().TrimEnd(Hyphen);
    }
}
