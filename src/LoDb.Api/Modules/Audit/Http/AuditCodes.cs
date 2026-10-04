namespace LoDb.Api.Modules.Audit.Http;

/// <summary>Reads a stored audit code without throwing on an unknown one.</summary>
internal static class AuditCodes
{
    /// <summary>
    /// Runs one of the <c>AuditVocabulary.Parse*</c> methods, which throw on a code outside
    /// their closed set; false for such a code or for none.
    /// </summary>
    public static bool TryParse<T>(string? code, Func<string, T> parse, out T value)
    {
        ArgumentNullException.ThrowIfNull(parse);
        value = default!;
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        try
        {
            value = parse(code);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>The code <paramref name="parse"/> reads, or <paramref name="fallback"/>.</summary>
    public static T OrDefault<T>(string? code, Func<string, T> parse, T fallback) =>
        TryParse(code, parse, out var value) ? value : fallback;
}
