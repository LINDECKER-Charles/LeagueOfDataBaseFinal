using System.Globalization;

namespace LoDb.Domain.Builds.Structures;

/// <summary>
/// Reads an integer written as text the way stored builds may carry it: canonical decimal
/// digits only, so "8000" and "-3" read while "08000", "+3", " 3" and "8000.0" do not.
/// </summary>
/// <remarks>
/// The legacy forms posted rune ids as strings: a stricter reading would turn builds already
/// saved into invalid ones, a looser one would accept ids no form ever wrote.
/// </remarks>
public static class IntegerText
{
    /// <summary>The integer <paramref name="text"/> writes; null when it writes none.</summary>
    public static int? Read(string? text)
    {
        if (string.IsNullOrEmpty(text)
            || !int.TryParse(
                text,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return null;
        }

        // Canonical only: the number written back must be the text itself.
        return string.Equals(
            value.ToString(CultureInfo.InvariantCulture),
            text,
            StringComparison.Ordinal)
            ? value
            : null;
    }
}
