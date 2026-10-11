using Microsoft.AspNetCore.Authentication.Cookies;

namespace LoDb.Api.Modules.Accounts.Authentication;

/// <summary>
/// Whether the session cookie authenticated the request, rather than merely came with it.
/// </summary>
/// <remarks>
/// The default scheme reads a bearer token in place of the cookie, but a policy that names
/// the cookie scheme authenticates the cookie whatever the <c>Authorization</c> header says.
/// The forgery guard then needs to know which of the two the request actually rests on.
/// </remarks>
internal static class SessionAuthentication
{
    // Private, so that only this class reads or writes the entry.
    private static readonly object Key = new();

    /// <summary>
    /// Runs <paramref name="validate"/>, Identity's check of the cookie, and marks the
    /// request when the principal comes through it.
    /// </summary>
    public static Func<CookieValidatePrincipalContext, Task> Marking(
        Func<CookieValidatePrincipalContext, Task> validate) =>
        async context =>
        {
            await validate(context);
            if (context.Principal is not null)
            {
                context.HttpContext.Items[Key] = true;
            }
        };

    /// <summary>Whether the session cookie authenticated <paramref name="context"/>.</summary>
    public static bool IsUsed(HttpContext context) => context.Items.ContainsKey(Key);
}
