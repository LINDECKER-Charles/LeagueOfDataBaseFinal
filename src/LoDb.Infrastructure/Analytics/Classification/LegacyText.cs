namespace LoDb.Infrastructure.Analytics.Classification;

/// <summary>
/// The string functions of PHP the legacy classification relies on, byte for byte: their
/// Unicode-aware .NET counterparts would classify a few headers differently.
/// </summary>
internal static class LegacyText
{
    // The characters PHP's trim() strips by default.
    private static readonly char[] TrimmedCharacters = [' ', '\t', '\n', '\r', '\0', '\v'];

    /// <summary>PHP's <c>trim</c>, with its default character list.</summary>
    public static string Trim(string? value) => (value ?? string.Empty).Trim(TrimmedCharacters);

    /// <summary>PHP's <c>strtolower</c>: ASCII letters only, whatever the culture.</summary>
    public static string Lower(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Create(value.Length, value, static (target, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                target[i] = char.IsAsciiLetterUpper(source[i])
                    ? char.ToLowerInvariant(source[i])
                    : source[i];
            }
        });
    }
}
