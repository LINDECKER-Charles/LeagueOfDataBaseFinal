namespace LoDb.Infrastructure.Audit;

/// <summary>
/// The closed set of audited actions: the 24 of the legacy journal, stored under their
/// legacy names (<see cref="AuditVocabulary.ToText(AuditAction)"/>).
/// </summary>
/// <remarks>
/// Only state changes and security events tied to an actor: page views belong to the
/// analytics. Values never change, since they are the event ids of the log mirror.
/// </remarks>
public enum AuditAction
{
    /// <summary><c>user.login</c>.</summary>
    UserLogin = 1,

    /// <summary><c>user.login_failed</c>, with the identifier typed in <c>identifier</c>.</summary>
    UserLoginFailed = 2,

    /// <summary><c>user.logout</c>.</summary>
    UserLogout = 3,

    /// <summary><c>user.register</c>.</summary>
    UserRegister = 4,

    /// <summary><c>user.password_reset</c>.</summary>
    UserPasswordReset = 5,

    /// <summary><c>user.email_verified</c>.</summary>
    UserEmailVerified = 6,

    /// <summary><c>profile.update</c>, the section changed in <c>section</c>.</summary>
    ProfileUpdate = 7,

    /// <summary><c>account.delete</c>.</summary>
    AccountDelete = 8,

    /// <summary><c>build.create</c>.</summary>
    BuildCreate = 9,

    /// <summary><c>build.update</c>.</summary>
    BuildUpdate = 10,

    /// <summary><c>build.delete</c>.</summary>
    BuildDelete = 11,

    /// <summary><c>build.vote</c>, the vote in <c>value</c>.</summary>
    BuildVote = 12,

    /// <summary><c>apikey.create</c>.</summary>
    ApiKeyCreate = 13,

    /// <summary><c>apikey.regenerate</c>.</summary>
    ApiKeyRegenerate = 14,

    /// <summary><c>apikey.revoke</c>.</summary>
    ApiKeyRevoke = 15,

    /// <summary><c>admin.user_ban</c>, the reason in <c>reason</c>.</summary>
    AdminUserBan = 16,

    /// <summary><c>admin.user_unban</c>.</summary>
    AdminUserUnban = 17,

    /// <summary><c>admin.user_delete</c>.</summary>
    AdminUserDelete = 18,

    /// <summary><c>admin.build_hide</c>.</summary>
    AdminBuildHide = 19,

    /// <summary><c>admin.build_delete</c>.</summary>
    AdminBuildDelete = 20,

    /// <summary><c>admin.api_client_revoke</c>.</summary>
    AdminApiClientRevoke = 21,

    /// <summary><c>admin.api_client_credit</c>, the credit in <c>requests</c>.</summary>
    AdminApiClientCredit = 22,

    /// <summary><c>admin.analytics_rollup</c>.</summary>
    AdminAnalyticsRollup = 23,

    /// <summary><c>admin.logs_purge</c>.</summary>
    AdminLogsPurge = 24,
}
