using System.Collections.Frozen;

namespace LoDb.Infrastructure.Audit;

/// <summary>
/// Stored spelling of the audit values, the one of the legacy journal: the columns of
/// <c>audit_log</c> and the event names of the log mirror use it.
/// </summary>
public static class AuditVocabulary
{
    private static readonly FrozenDictionary<AuditAction, string> ActionNames =
        new Dictionary<AuditAction, string>
        {
            [AuditAction.UserLogin] = "user.login",
            [AuditAction.UserLoginFailed] = "user.login_failed",
            [AuditAction.UserLogout] = "user.logout",
            [AuditAction.UserRegister] = "user.register",
            [AuditAction.UserPasswordReset] = "user.password_reset",
            [AuditAction.UserEmailVerified] = "user.email_verified",
            [AuditAction.ProfileUpdate] = "profile.update",
            [AuditAction.AccountDelete] = "account.delete",
            [AuditAction.BuildCreate] = "build.create",
            [AuditAction.BuildUpdate] = "build.update",
            [AuditAction.BuildDelete] = "build.delete",
            [AuditAction.BuildVote] = "build.vote",
            [AuditAction.ApiKeyCreate] = "apikey.create",
            [AuditAction.ApiKeyRegenerate] = "apikey.regenerate",
            [AuditAction.ApiKeyRevoke] = "apikey.revoke",
            [AuditAction.AdminUserBan] = "admin.user_ban",
            [AuditAction.AdminUserUnban] = "admin.user_unban",
            [AuditAction.AdminUserDelete] = "admin.user_delete",
            [AuditAction.AdminBuildHide] = "admin.build_hide",
            [AuditAction.AdminBuildDelete] = "admin.build_delete",
            [AuditAction.AdminApiClientRevoke] = "admin.api_client_revoke",
            [AuditAction.AdminApiClientCredit] = "admin.api_client_credit",
            [AuditAction.AdminAnalyticsRollup] = "admin.analytics_rollup",
            [AuditAction.AdminLogsPurge] = "admin.logs_purge",
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, AuditAction> ActionsByName =
        ActionNames.ToFrozenDictionary(
            static entry => entry.Value,
            static entry => entry.Key,
            StringComparer.Ordinal);

    public static string ToText(AuditAction action) =>
        ActionNames.TryGetValue(action, out var name)
            ? name
            : throw new ArgumentOutOfRangeException(nameof(action), action, null);

    public static AuditAction ParseAction(string value) =>
        ActionsByName.TryGetValue(value, out var action)
            ? action
            : throw new ArgumentOutOfRangeException(nameof(value), value, null);

    public static string ToText(AuditOutcome outcome) => outcome switch
    {
        AuditOutcome.Success => "success",
        AuditOutcome.Failure => "failure",
        AuditOutcome.Denied => "denied",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };

    public static AuditOutcome ParseOutcome(string value) => value switch
    {
        "success" => AuditOutcome.Success,
        "failure" => AuditOutcome.Failure,
        "denied" => AuditOutcome.Denied,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static string ToText(AuditActorType type) => type switch
    {
        AuditActorType.User => "user",
        AuditActorType.Admin => "admin",
        AuditActorType.Anonymous => "anonymous",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static AuditActorType ParseActorType(string value) => value switch
    {
        "user" => AuditActorType.User,
        "admin" => AuditActorType.Admin,
        "anonymous" => AuditActorType.Anonymous,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static string ToText(AuditTargetType type) => type switch
    {
        AuditTargetType.User => "user",
        AuditTargetType.Build => "build",
        AuditTargetType.ApiKey => "apikey",
        AuditTargetType.ApiClient => "api_client",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static AuditTargetType ParseTargetType(string value) => value switch
    {
        "user" => AuditTargetType.User,
        "build" => AuditTargetType.Build,
        "apikey" => AuditTargetType.ApiKey,
        "api_client" => AuditTargetType.ApiClient,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
