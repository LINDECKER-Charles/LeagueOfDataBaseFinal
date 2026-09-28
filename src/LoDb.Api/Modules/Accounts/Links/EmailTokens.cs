using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>
/// Identity's e-mail tokens as a link carries them: the base64url of their text, whose own
/// characters a query string would mangle.
/// </summary>
internal static class EmailTokens
{
    public static string Encode(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    /// <summary>The token of a link, or null when the link is damaged.</summary>
    public static string? Decode(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded.Trim()));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
