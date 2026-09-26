using Microsoft.AspNetCore.Authorization;

namespace LoDb.Api.Modules.Accounts.Protection;

/// <summary>
/// The signed-in account has verified its e-mail and is not banned, as stored now rather
/// than when the session was opened.
/// </summary>
internal sealed class VerifiedEmailRequirement : IAuthorizationRequirement
{
    public static VerifiedEmailRequirement Instance { get; } = new();
}
