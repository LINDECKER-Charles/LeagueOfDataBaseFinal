using System.Text.RegularExpressions;

namespace LoDb.Api.Modules.Accounts.SignIn;

/// <summary>
/// The shapes the fields of a sign-in may take, checked before the account is looked up: a
/// value past them names no account and holds no code.
/// </summary>
internal static partial class SignInFieldBounds
{
    /// <summary>The longest e-mail address SMTP carries (RFC 5321), above every username.</summary>
    public const int IdentifierMaxLength = 254;

    /// <summary>Whether <paramref name="identifier"/> can name an account.</summary>
    public static bool IsIdentifier(string identifier) =>
        identifier.Trim().Length <= IdentifierMaxLength;

    /// <summary>
    /// Whether <paramref name="code"/> can be an authenticator code: six digits, which the
    /// apps group with spaces or a dash, within room to spare.
    /// </summary>
    public static bool IsAuthenticatorCode(string code) => AuthenticatorCode().IsMatch(code);

    /// <summary>
    /// Whether <paramref name="code"/> can be a recovery code, such as <c>AB3CD-EF4GH</c>,
    /// typed in any case.
    /// </summary>
    public static bool IsRecoveryCode(string code) => RecoveryCode().IsMatch(code);

    [GeneratedRegex(@"\A[0-9 -]{1,16}\z", RegexOptions.CultureInvariant)]
    private static partial Regex AuthenticatorCode();

    [GeneratedRegex(@"\A[A-Za-z0-9 -]{1,32}\z", RegexOptions.CultureInvariant)]
    private static partial Regex RecoveryCode();
}
