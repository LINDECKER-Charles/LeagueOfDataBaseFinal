using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Desktop.Auth.Google;

/// <summary>
/// Google's authorization request for an installed app: authorization code with PKCE and a
/// loopback redirect (RFC 8252, RFC 7636), opened in the system browser.
/// </summary>
internal static class GoogleAuthorization
{
    public const string Endpoint = "https://accounts.google.com/o/oauth2/v2/auth";

    // 256 bits, 43 characters once encoded: the shortest verifier RFC 7636 accepts.
    private const int SecretBytes = 32;

    private const string ClientIdKey = "client_id";
    private const string RedirectUriKey = "redirect_uri";
    private const string ResponseTypeKey = "response_type";
    private const string ScopeKey = "scope";
    private const string StateKey = "state";
    private const string PromptKey = "prompt";
    private const string CodeResponse = "code";
    private const string Scope = "openid email profile";

    // Lets a user with several Google accounts pick one, instead of the browser's current.
    private const string AccountChooser = "select_account";

    /// <summary>A random URL-safe secret, used as a PKCE verifier or an OAuth state.</summary>
    public static string NewSecret() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));

    public static string ChallengeOf(string verifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static Uri UrlFor(string clientId, GoogleFlow flow) =>
        new(QueryHelpers.AddQueryString(Endpoint, new Dictionary<string, string?>
        {
            [ClientIdKey] = clientId,
            [RedirectUriKey] = flow.RedirectUri,
            [ResponseTypeKey] = CodeResponse,
            [ScopeKey] = Scope,
            [OAuthConstants.CodeChallengeKey] = ChallengeOf(flow.Verifier),
            [OAuthConstants.CodeChallengeMethodKey] = OAuthConstants.CodeChallengeMethodS256,
            [StateKey] = flow.State,
            [PromptKey] = AccountChooser,
        }));
}
