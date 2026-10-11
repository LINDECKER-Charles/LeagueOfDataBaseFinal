namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>What one run of <see cref="DatabaseRoles"/> changed on the server's roles.</summary>
/// <param name="Created">Roles that did not exist.</param>
/// <param name="Altered">
/// Existing roles whose attributes, or the API's statement timeout, were set again.
/// </param>
/// <param name="PasswordSet">Whether the API's password changed.</param>
public sealed record RoleChanges(int Created, int Altered, bool PasswordSet)
{
    /// <summary>Nothing changed: every role was already as configured.</summary>
    public bool IsEmpty => Created == 0 && Altered == 0 && !PasswordSet;
}
