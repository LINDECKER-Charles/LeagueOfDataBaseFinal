using System.Globalization;

namespace LoDb.Api.Modules.Admin.Mfa;

/// <summary>
/// The <c>otpauth://</c> URI an authenticator app reads from a QR code: the account, its
/// shared key and the issuer shown next to the codes.
/// </summary>
/// <remarks>
/// Identity's authenticator provider checks six-digit codes of thirty seconds with SHA-1,
/// the defaults of the format, so only the digits are stated.
/// </remarks>
internal static class AuthenticatorUri
{
    public const string Issuer = "LoDb";

    private const int Digits = 6;

    public static string For(string account, string sharedKey)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(sharedKey);
        var issuer = Uri.EscapeDataString(Issuer);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"otpauth://totp/{issuer}:{Uri.EscapeDataString(account)}"
            + $"?secret={sharedKey}&issuer={issuer}&digits={Digits}");
    }
}
