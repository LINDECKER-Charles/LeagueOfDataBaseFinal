namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>
/// What <c>migrate</c> is told of the roles that sign in: the API's and the backups'.
/// </summary>
internal sealed record RoleSettings(AppRoleOptions App, BackupRoleOptions Backup);
