using System.Globalization;
using System.Text;

namespace LoDb.Api.Modules.PublicApi.Http;

/// <summary>
/// Reads a query string as go-api does (<c>url.ParseQuery</c>, then <c>Values.Get</c>),
/// which ASP.NET's binding does not: <c>/v1</c> clients rely on its quirks.
/// </summary>
/// <remarks>
/// Names are case-sensitive and the first value wins; a pair holding a semicolon, or a
/// malformed escape in its name or value, is skipped; <c>+</c> reads as a space.
/// </remarks>
internal static class GoQuery
{
    private const char QueryMark = '?';
    private const char PairSeparator = '&';
    private const char Assignment = '=';
    private const char Semicolon = ';';
    private const byte Escape = (byte)'%';
    private const byte Plus = (byte)'+';
    private const byte Space = (byte)' ';

    /// <summary>The first value of <paramref name="name"/>, or an empty string.</summary>
    public static string Get(QueryString query, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var raw = query.Value ?? string.Empty;
        var rest = raw.StartsWith(QueryMark) ? raw.AsSpan(1) : raw.AsSpan();
        while (!rest.IsEmpty)
        {
            var end = rest.IndexOf(PairSeparator);
            var pair = end < 0 ? rest : rest[..end];
            rest = end < 0 ? [] : rest[(end + 1)..];
            if (pair.IsEmpty || pair.Contains(Semicolon))
            {
                continue;
            }

            var assignment = pair.IndexOf(Assignment);
            var rawName = assignment < 0 ? pair : pair[..assignment];
            var rawValue = assignment < 0 ? [] : pair[(assignment + 1)..];
            if (TryUnescape(rawName, out var key)
                && TryUnescape(rawValue, out var value)
                && string.Equals(key, name, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return string.Empty;
    }

    // Go's QueryUnescape: "%" must start two hexadecimal digits; the bytes read as UTF-8.
    private static bool TryUnescape(ReadOnlySpan<char> text, out string result)
    {
        result = string.Empty;
        var source = Encoding.UTF8.GetBytes(text.ToString());
        var bytes = new byte[source.Length];
        var length = 0;
        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            if (current == Escape)
            {
                if (index + 2 >= source.Length
                    || !IsHex(source[index + 1])
                    || !IsHex(source[index + 2]))
                {
                    return false;
                }

                bytes[length++] = byte.Parse(
                    source.AsSpan(index + 1, 2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture);
                index += 2;
            }
            else
            {
                bytes[length++] = current == Plus ? Space : current;
            }
        }

        result = Encoding.UTF8.GetString(bytes, 0, length);
        return true;
    }

    private static bool IsHex(byte value) => char.IsAsciiHexDigit((char)value);
}
