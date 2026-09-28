using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Authentication;

/// <summary>
/// Where a sign-in lands: the session cookie of the web, or the bearer and refresh tokens of
/// the apps, written in the response body by Identity's bearer handler.
/// </summary>
/// <param name="Scheme">The scheme the sign-in manager signs in with.</param>
/// <param name="AuditName">The <c>channel</c> recorded with <c>user.login</c>.</param>
internal sealed record SignInChannel(string Scheme, string AuditName)
{
    public static SignInChannel Session { get; } =
        new(IdentityConstants.ApplicationScheme, "session");

    public static SignInChannel Token { get; } = new(IdentityConstants.BearerScheme, "token");
}
