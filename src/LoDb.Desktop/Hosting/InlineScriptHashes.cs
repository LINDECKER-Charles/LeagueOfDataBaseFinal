using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LoDb.Desktop.Hosting;

/// <summary>
/// The CSP sources (<c>'sha256-…'</c>) of the inline scripts a page runs, as the web's
/// renderer computes them (src/LoDb.Web/src/server/security/csp).
/// </summary>
internal static partial class InlineScriptHashes
{
    private const string NoType = "";

    /// <summary>The hashes of the scripts of <paramref name="html"/>, in document order.</summary>
    public static IReadOnlyList<string> Of(string html) =>
        ScriptElement().Matches(html)
            .Where(static script => IsExecutableInline(script.Groups["attributes"].Value))
            .Select(static script => OfScript(script.Groups["text"].Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <summary>The hash that admits an inline script of exactly <paramref name="text"/>.</summary>
    public static string OfScript(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return $"'sha256-{Convert.ToBase64String(digest)}'";
    }

    // A script with a source runs no inline code; a data block (JSON) runs nothing at all.
    private static bool IsExecutableInline(string attributes)
    {
        if (SourceAttribute().IsMatch(attributes))
        {
            return false;
        }

        var type = TypeAttribute().Match(attributes);
        var value = type.Success ? type.Groups["type"].Value.Split(';')[0].Trim() : NoType;
        return value.Length == 0 || ExecutableType().IsMatch(value);
    }

    [GeneratedRegex(@"<script\b(?<attributes>[^>]*)>(?<text>[\s\S]*?)</script\s*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex ScriptElement();

    [GeneratedRegex(@"(?:^|\s)src\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex SourceAttribute();

    [GeneratedRegex(@"(?:^|\s)type\s*=\s*[""']?(?<type>[^""'\s>]*)", RegexOptions.IgnoreCase)]
    private static partial Regex TypeAttribute();

    [GeneratedRegex(
        @"^(?:module|(?:text|application)/(?:x-)?(?:java|ecma)script|text/javascript1\.[0-5])$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExecutableType();
}
