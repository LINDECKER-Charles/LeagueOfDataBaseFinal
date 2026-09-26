using Microsoft.AspNetCore.Identity;

namespace LoDb.Infrastructure.Persistence.Accounts;

/// <summary>A row of <c>identity_roles</c>: an Identity role of the rewrite.</summary>
/// <remarks>
/// Kept apart from the legacy JSON column <c>users.roles</c>, which only the legacy stack
/// reads. Roles are granted by hand or by the admin lot: the migration seeds none.
/// </remarks>
public sealed class Role : IdentityRole<int>
{
    /// <summary>The administrators, who also need MFA (policy <c>Admin</c>).</summary>
    public const string Admin = "Admin";
}
