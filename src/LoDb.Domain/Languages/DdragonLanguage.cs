using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace LoDb.Domain.Languages;

/// <summary>
/// A Data Dragon language code ("fr_FR", "en_US"), as <c>languages.json</c> lists it.
/// </summary>
/// <remarks>
/// The set of languages is data, read per version: the domain only checks the shape, which
/// also makes the code safe as a path segment of the datasets and the storage keys.
/// <see cref="EnUs"/> is the one language every version ships, hence the only universal
/// fallback (UP 2).
/// </remarks>
public sealed partial record DdragonLanguage
{
    /// <summary>Shape of a language code for the API contract and the front.</summary>
    public const string Pattern = "[a-z]{2}_[A-Z]{2}";

    private DdragonLanguage(string code) => Code = code;

    /// <summary>English (United States), shipped with every Data Dragon version.</summary>
    public static DdragonLanguage EnUs { get; } = new("en_US");

    /// <summary>The code as Data Dragon writes it.</summary>
    public string Code { get; }

    public static bool TryParse(
        [NotNullWhen(true)] string? text,
        [NotNullWhen(true)] out DdragonLanguage? language)
    {
        language = text is not null && WellFormed().IsMatch(text)
            ? new DdragonLanguage(text)
            : null;
        return language is not null;
    }

    public static DdragonLanguage Parse(string text) =>
        TryParse(text, out var language)
            ? language
            : throw new FormatException($"'{text}' is not a Data Dragon language code.");

    public override string ToString() => Code;

    [GeneratedRegex(@"^[a-z]{2}_[A-Z]{2}\z")]
    private static partial Regex WellFormed();
}
