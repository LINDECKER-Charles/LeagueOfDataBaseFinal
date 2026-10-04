using Microsoft.AspNetCore.Authorization;

namespace LoDb.Api.Modules.Accounts.Protection;

/// <summary>
/// The request is not forged by another site: an unsafe <c>/api</c> request comes from a
/// trusted origin, and when a session cookie authenticates it, it carries the XSRF token.
/// </summary>
/// <remarks>
/// Part of every named policy and of the fallback one, so that no endpoint of the API goes
/// without it. An endpoint marked <c>AllowAnonymous</c>, or given a policy built in place,
/// skips it: unsafe endpoints use neither.
/// </remarks>
internal sealed class ForgeryRequirement : IAuthorizationRequirement
{
    public static ForgeryRequirement Instance { get; } = new();
}
