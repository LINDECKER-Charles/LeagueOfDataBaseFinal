using System.Globalization;
using System.Text;

namespace LoDb.Infrastructure.PageStore.Signing;

/// <summary>
/// The URI encoding of Signature Version 4: every byte of the UTF-8 form escaped as an
/// upper-case <c>%XX</c>, except the RFC 3986 unreserved characters.
/// </summary>
/// <remarks>
/// <see cref="Uri.EscapeDataString(string)"/> is close but not equal: the signature is
/// computed over the exact bytes the store re-encodes, so the rule is written out here.
/// </remarks>
internal static class SigV4Encoding
{
    private const string UnreservedSymbols = "-_.~";

    /// <summary>One path segment or query component, encoded.</summary>
    public static string Encode(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var octet in Encoding.UTF8.GetBytes(value))
        {
            if (IsUnreserved(octet))
            {
                builder.Append((char)octet);
            }
            else
            {
                builder.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The canonical form of a path as the store reads it: each segment decoded, then encoded
    /// again, so a path sent with or without escapes signs the same.
    /// </summary>
    public static string CanonicalPath(string absolutePath)
    {
        var segments = absolutePath.Split('/')
            .Select(static segment => Encode(Uri.UnescapeDataString(segment)));
        return string.Join('/', segments);
    }

    private static bool IsUnreserved(byte octet) =>
        char.IsAsciiLetterOrDigit((char)octet) || UnreservedSymbols.Contains((char)octet);
}
