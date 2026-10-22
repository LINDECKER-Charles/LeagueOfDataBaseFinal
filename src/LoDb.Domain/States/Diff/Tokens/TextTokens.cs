using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using LoDb.Domain.Languages;

namespace LoDb.Domain.States.Diff.Tokens;

/// <summary>
/// A plain text cut into the tokens a diff aligns: words, runs of whitespace and single
/// punctuation marks; each text element (<see cref="StringInfo"/>) in the languages that do
/// not separate words with spaces.
/// </summary>
/// <remarks>
/// Every token is a whole number of text elements, so no segment ever splits a surrogate pair
/// or a combining sequence.
/// </remarks>
internal static class TextTokens
{
    // Japanese, Chinese and Thai write words without spaces between them.
    private static readonly FrozenSet<string> UnspacedLanguages =
        FrozenSet.Create(StringComparer.Ordinal, "ja_JP", "zh_CN", "zh_TW", "th_TH");

    private enum TokenClass
    {
        Word,
        Space,
        Mark,
    }

    public static IReadOnlyList<string> Of(string text, DdragonLanguage language) =>
        UnspacedLanguages.Contains(language.Code) ? Elements(text) : Words(text);

    private static List<string> Elements(string text)
    {
        List<string> elements = [];
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        return elements;
    }

    // Consecutive elements of a word or of a whitespace run join; a mark stands alone.
    private static List<string> Words(string text)
    {
        List<string> tokens = [];
        var current = new StringBuilder();
        TokenClass? currentClass = null;
        foreach (var element in Elements(text))
        {
            var elementClass = ClassOf(element);
            if (elementClass != currentClass || elementClass == TokenClass.Mark)
            {
                Flush(tokens, current);
                currentClass = elementClass;
            }

            current.Append(element);
        }

        Flush(tokens, current);
        return tokens;
    }

    private static void Flush(List<string> tokens, StringBuilder current)
    {
        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
            current.Clear();
        }
    }

    private static TokenClass ClassOf(string element)
    {
        if (char.IsWhiteSpace(element, 0))
        {
            return TokenClass.Space;
        }

        return char.IsLetterOrDigit(element, 0) ? TokenClass.Word : TokenClass.Mark;
    }
}
