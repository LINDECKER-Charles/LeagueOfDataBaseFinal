using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace LoDb.Infrastructure.Persistence.Accounts;

/// <summary>Identity's EF store over <c>identity_roles</c>, without role claims.</summary>
/// <remarks>
/// Role claims have no table: the claims factory asks for them at every sign-in and finds
/// none; adding one is refused.
/// </remarks>
internal sealed class LoDbRoleStore(LoDbDbContext context, IdentityErrorDescriber? describer = null)
    : RoleStore<Role, LoDbDbContext, int, IdentityUserRole<int>, IdentityRoleClaim<int>>(
        context,
        describer)
{
    public override Task<IList<Claim>> GetClaimsAsync(
        Role role,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IList<Claim>>([]);

    public override Task AddClaimAsync(
        Role role,
        Claim claim,
        CancellationToken cancellationToken = default) =>
        throw Unmapped();

    public override Task RemoveClaimAsync(
        Role role,
        Claim claim,
        CancellationToken cancellationToken = default) =>
        throw Unmapped();

    private static NotSupportedException Unmapped() =>
        new("The LoDb schema maps no Identity role claims.");
}
