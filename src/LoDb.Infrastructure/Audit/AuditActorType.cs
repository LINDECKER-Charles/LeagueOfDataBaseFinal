namespace LoDb.Infrastructure.Audit;

/// <summary>
/// Who performed an audited action, stored as <c>user</c>, <c>admin</c> or
/// <c>anonymous</c>.
/// </summary>
public enum AuditActorType
{
    /// <summary>A signed-in account.</summary>
    User,

    /// <summary>A signed-in account in the <c>Admin</c> role.</summary>
    Admin,

    /// <summary>Nobody signed in: registration, password reset, failed sign-in.</summary>
    Anonymous,
}
