using System.Text.RegularExpressions;

namespace LoDb.Api.Modules.Accounts.Google.Apps;

/// <summary>
/// The shapes of the code and of the PKCE verifier an app hands over, checked before the
/// code is sent to Google: a value Google could only refuse never leaves the API.
/// </summary>
internal static partial class GoogleExchangeShape
{
    /// <summary>
    /// Whether <paramref name="code"/> can be an authorization code: visible ASCII (RFC 6749,
    /// appendix A.11), within a bound far above Google's codes.
    /// </summary>
    public static bool IsCode(string code) => CodePattern().IsMatch(code);

    /// <summary>
    /// Whether <paramref name="verifier"/> is a PKCE verifier: 43 to 128 unreserved
    /// characters (RFC 7636, section 4.1).
    /// </summary>
    public static bool IsVerifier(string verifier) => VerifierPattern().IsMatch(verifier);

    [GeneratedRegex(@"\A[\x21-\x7E]{1,512}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"\A[A-Za-z0-9._~-]{43,128}\z", RegexOptions.CultureInvariant)]
    private static partial Regex VerifierPattern();
}
