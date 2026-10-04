using System.Globalization;
using System.Text;

namespace LoDb.Domain.Text;

/// <summary>
/// Accent folding, for slugs and accent-insensitive search ("Épée" matches "epee").
/// </summary>
public static class TextFolding
{
    /// <summary>
    /// Removes the combining marks of every decomposable letter ("é" becomes "e"). Letters
    /// without a decomposition ("ø", "ß") are kept as they are.
    /// </summary>
    public static string RemoveDiacritics(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(character);
            }
        }

        return folded.ToString().Normalize(NormalizationForm.FormC);
    }
}
