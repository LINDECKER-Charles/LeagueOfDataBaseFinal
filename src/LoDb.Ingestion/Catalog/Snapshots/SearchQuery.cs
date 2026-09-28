using System.Diagnostics.CodeAnalysis;
using LoDb.Domain.Text;

namespace LoDb.Ingestion.Catalog.Snapshots;

/// <summary>
/// A free-text search: from 2 to 50 characters once trimmed, matched without accents or case
/// ("epee" finds "Épée").
/// </summary>
/// <remarks>
/// The bounds are the legacy ones, which keep one-letter floods out; characters are counted
/// as code points, as <c>mb_strlen</c> did.
/// </remarks>
public sealed record SearchQuery
{
    public const int MinLength = 2;
    public const int MaxLength = 50;

    private SearchQuery(string text)
    {
        Text = text;
        Needle = Fold(text);
    }

    /// <summary>The query as typed, trimmed.</summary>
    public string Text { get; }

    /// <summary>The folded form looked for in the folded ids and names.</summary>
    internal string Needle { get; }

    public static bool TryParse(
        [NotNullWhen(true)] string? text,
        [NotNullWhen(true)] out SearchQuery? query)
    {
        var trimmed = text?.Trim();
        var length = trimmed?.EnumerateRunes().Count() ?? 0;
        query = trimmed is not null && length is >= MinLength and <= MaxLength
            ? new SearchQuery(trimmed)
            : null;
        return query is not null;
    }

    /// <exception cref="FormatException">The text is shorter or longer than allowed.</exception>
    public static SearchQuery Parse(string text) =>
        TryParse(text, out var query)
            ? query
            : throw new FormatException(
                $"A search takes {MinLength} to {MaxLength} characters.");

    /// <summary>Accents removed, then lowercased whatever the culture.</summary>
    internal static string Fold(string text) =>
        TextFolding.RemoveDiacritics(text).ToLowerInvariant();
}
