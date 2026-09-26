using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LoDb.Api.Modules.ClientPolicy;

/// <summary>An app's release number: three numbers, as the release tags write it (1.2.3).</summary>
internal static class AppVersion
{
    private const int Parts = 3;

    public static bool TryParse(string? text, [NotNullWhen(true)] out Version? version)
    {
        var parts = text?.Split('.') ?? [];
        version = parts.Length == Parts && parts.All(IsNumber)
            && Version.TryParse(text, out var parsed)
                ? parsed
                : null;
        return version is not null;
    }

    // Version.TryParse alone takes signs and blanks around each number.
    private static bool IsNumber(string part) =>
        part.Length > 0
        && part.All(char.IsAsciiDigit)
        && int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _);
}
