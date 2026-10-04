using System.Text;
using System.Text.RegularExpressions;

namespace LoDb.Infrastructure.Outbox.Rendering;

/// <summary>
/// The few rules the e-mail templates need: <c>{{name}}</c> writes a value,
/// <c>{{#name}}…{{/name}}</c> keeps its content only when the value is not empty, and
/// <c>{{! … }}</c> is a comment.
/// </summary>
/// <remarks>
/// Values are escaped in an HTML template, written as they are in a text one. A name the
/// values do not hold is a mistake of the template, never of the model: it throws.
/// </remarks>
internal static partial class TemplateEngine
{
    public static string Render(
        string template,
        IReadOnlyDictionary<string, string> values,
        bool html)
    {
        var withoutComments = Comment().Replace(template, string.Empty);
        var withSections = Section().Replace(
            withoutComments,
            match => Value(values, match.Groups["name"].Value).Length > 0
                ? match.Groups["body"].Value
                : string.Empty);
        return Variable().Replace(withSections, match =>
        {
            var value = Value(values, match.Groups["name"].Value);
            return html ? EscapeHtml(value) : value;
        });
    }

    /// <summary>Escapes the five characters that matter in HTML text and attributes.</summary>
    public static string EscapeHtml(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            escaped.Append(character switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => character.ToString(),
            });
        }

        return escaped.ToString();
    }

    private static string Value(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value)
            ? value
            : throw new InvalidOperationException($"The template uses {name}, which has no value.");

    [GeneratedRegex(@"\{\{!.*?\}\}", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Comment();

    [GeneratedRegex(
        @"\{\{#(?<name>\w+)\}\}(?<body>.*?)\{\{/\k<name>\}\}",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Section();

    [GeneratedRegex(@"\{\{(?<name>\w+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Variable();
}
