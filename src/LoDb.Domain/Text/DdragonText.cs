using System.Text;
using System.Text.RegularExpressions;

namespace LoDb.Domain.Text;

/// <summary>
/// Cleanup of Data Dragon copy, one rule for every surface (pages, JSON-LD, search).
/// </summary>
/// <remarks>
/// The regular expressions spell ASCII classes out: the legacy rules ran byte-wise, where
/// "\w" and "\s" are ASCII only, while .NET's match every Unicode letter and space.
/// </remarks>
public static partial class DdragonText
{
    private const char MarkupStart = '<';
    private const string SingleSpace = " ";

    // PHP's trim() set: NBSP and other Unicode spaces are content, not padding.
    private const string TrimmedCharacters = " \t\n\r\0\v";

    /// <summary>
    /// Removes the template tokens Data Dragon leaks without ever resolving them
    /// ("{{ Item_Cooldown }}", "@BaseHeal@"): they carry no displayable value. Markup stays.
    /// </summary>
    public static string Clean(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var withoutTemplates = TemplateToken().Replace(html, string.Empty);
        var stripped = VariableToken().Replace(withoutTemplates, string.Empty);
        return Trim(RepeatedBlanks().Replace(stripped, SingleSpace));
    }

    /// <summary>
    /// Display form of a category tag: "CriticalStrike" reads "Critical Strike". Matching
    /// keeps the raw tag.
    /// </summary>
    public static string TagLabel(string? tag) =>
        CamelCaseBoundary().Replace(tag ?? string.Empty, "$1 $2");

    /// <summary>
    /// A display name out of a field Riot sometimes ships as marked-up copy: the name proper
    /// is what precedes the first line break. Plain names pass through, trimmed.
    /// </summary>
    public static string PlainName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        if (!name.Contains(MarkupStart, StringComparison.Ordinal))
        {
            return Trim(name);
        }

        var head = LineBreak().Split(name, 2)[0];
        return Trim(Tag().Replace(Clean(head), string.Empty));
    }

    /// <summary>
    /// Uppercases the first letter and keeps the rest intact: Data Dragon writes some titles
    /// in lowercase ("force de Demacia"), and lowercasing the rest would break proper nouns.
    /// </summary>
    public static string UpperFirst(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        Rune.DecodeFromUtf16(text, out var first, out var length);
        return string.Concat(Rune.ToUpperInvariant(first).ToString(), text.AsSpan(length));
    }

    private static string Trim(string text) => text.AsSpan().Trim(TrimmedCharacters).ToString();

    [GeneratedRegex(@"\{\{[^{}]*\}\}")]
    private static partial Regex TemplateToken();

    [GeneratedRegex(@"@[A-Za-z0-9_.]+@")]
    private static partial Regex VariableToken();

    [GeneratedRegex("[ \t]{2,}")]
    private static partial Regex RepeatedBlanks();

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex CamelCaseBoundary();

    [GeneratedRegex(
        @"<br[ \t\n\r\v\f]*/?[ \t\n\r\v\f]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LineBreak();

    // An unclosed tag runs to the end of the text, as strip_tags() reads it.
    [GeneratedRegex(@"<[^>]*(?:>|\z)")]
    private static partial Regex Tag();
}
