using LoDb.Domain.Languages;
using LoDb.Domain.States.Diff.Tokens;

namespace LoDb.Domain.States.Diff;

/// <summary>
/// The diff of two plain texts, token by token: words (split on whitespace and
/// punctuation) by default, text elements (<c>StringInfo</c>) for the languages that do not
/// separate words, ja_JP, zh_CN, zh_TW and th_TH.
/// </summary>
public static class TextDiff
{
    /// <summary>
    /// The segments that turn <paramref name="before"/> into <paramref name="after"/>, both in
    /// <c>DdragonText.PlainText</c> form; one <see cref="SegmentKind.Same"/> segment when equal.
    /// </summary>
    public static IReadOnlyList<TextSegment> Of(
        string before,
        string after,
        DdragonLanguage language)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(language);
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return [new TextSegment { Kind = SegmentKind.Same, Text = after }];
        }

        return TokenAlignment.Of(
            TextTokens.Of(before, language),
            TextTokens.Of(after, language));
    }
}
