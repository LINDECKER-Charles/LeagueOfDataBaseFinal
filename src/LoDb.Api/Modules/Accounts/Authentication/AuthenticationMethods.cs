using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Authentication;

/// <summary>
/// How an account signed in: the <c>amr</c> claim Identity writes (<c>pwd</c>, <c>mfa</c>)
/// and the external provider, which a refreshed principal must keep.
/// </summary>
/// <remarks>
/// Identity rebuilds the principal from the account when it revalidates a session or a
/// refresh token, and drops these claims: the Admin policy would then lose <c>mfa</c>.
/// </remarks>
internal static class AuthenticationMethods
{
    public const string ClaimType = "amr";
    public const string Password = "pwd";
    public const string MultiFactor = "mfa";

    public static bool IsMultiFactor(ClaimsPrincipal principal) =>
        principal.HasClaim(ClaimType, MultiFactor);

    /// <summary>Copies the sign-in methods of <paramref name="source"/> onto another.</summary>
    public static void Carry(ClaimsPrincipal source, ClaimsPrincipal target)
    {
        if (target.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        foreach (var claim in source.Claims.Where(static claim => IsMethod(claim.Type)).ToList())
        {
            identity.AddClaim(new Claim(claim.Type, claim.Value));
        }
    }

    /// <summary>Keeps the sign-in methods when a session cookie is revalidated.</summary>
    public static Task KeepOnRefreshAsync(SecurityStampRefreshingPrincipalContext context)
    {
        if (context.CurrentPrincipal is { } current && context.NewPrincipal is { } refreshed)
        {
            Carry(current, refreshed);
        }

        return Task.CompletedTask;
    }

    private static bool IsMethod(string type) =>
        type is ClaimType or ClaimTypes.AuthenticationMethod;
}
