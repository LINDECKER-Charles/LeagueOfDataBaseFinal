using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace LoDb.Desktop.Bridge.Validation;

/// <summary>A media type as RFC 6838 names them: <c>type/subtype</c>, without parameters.</summary>
internal static partial class MimeType
{
    public static bool IsValid([NotNullWhen(true)] string? value) =>
        value is not null && Pattern().IsMatch(value);

    [GeneratedRegex(
        "^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]{0,126}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
