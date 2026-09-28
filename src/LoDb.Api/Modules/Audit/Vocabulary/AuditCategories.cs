using System.Collections.Frozen;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Modules.Audit.Vocabulary;

/// <summary>
/// The coarse groups of the admin filter (<c>auth</c>, <c>account</c>, <c>build</c>,
/// <c>apikey</c>, <c>admin</c>), those of the legacy journal.
/// </summary>
/// <remarks>
/// An explicit table rather than the dotted prefix, as in the legacy stack: an action added
/// without a group fails the vocabulary test instead of being filed anywhere.
/// </remarks>
internal static class AuditCategories
{
    public const string Auth = "auth";
    public const string Account = "account";
    public const string Build = "build";
    public const string ApiKey = "apikey";
    public const string Admin = "admin";

    private static readonly FrozenDictionary<AuditAction, string> Groups =
        new Dictionary<AuditAction, string>
        {
            [AuditAction.UserLogin] = Auth,
            [AuditAction.UserLoginFailed] = Auth,
            [AuditAction.UserLogout] = Auth,
            [AuditAction.UserRegister] = Account,
            [AuditAction.UserPasswordReset] = Account,
            [AuditAction.UserEmailVerified] = Account,
            [AuditAction.ProfileUpdate] = Account,
            [AuditAction.AccountDelete] = Account,
            [AuditAction.BuildCreate] = Build,
            [AuditAction.BuildUpdate] = Build,
            [AuditAction.BuildDelete] = Build,
            [AuditAction.BuildVote] = Build,
            [AuditAction.ApiKeyCreate] = ApiKey,
            [AuditAction.ApiKeyRegenerate] = ApiKey,
            [AuditAction.ApiKeyRevoke] = ApiKey,
            [AuditAction.AdminUserBan] = Admin,
            [AuditAction.AdminUserUnban] = Admin,
            [AuditAction.AdminUserDelete] = Admin,
            [AuditAction.AdminBuildHide] = Admin,
            [AuditAction.AdminBuildDelete] = Admin,
            [AuditAction.AdminApiClientRevoke] = Admin,
            [AuditAction.AdminApiClientCredit] = Admin,
            [AuditAction.AdminAnalyticsRollup] = Admin,
            [AuditAction.AdminLogsPurge] = Admin,
        }.ToFrozenDictionary();

    /// <summary>The groups, in the order of the admin filter.</summary>
    public static IReadOnlyList<string> All { get; } = [Auth, Account, Build, ApiKey, Admin];

    /// <summary>The group of <paramref name="action"/>.</summary>
    public static string Of(AuditAction action) =>
        Groups.TryGetValue(action, out var group)
            ? group
            : throw new ArgumentOutOfRangeException(nameof(action), action, null);

    /// <summary>The actions of <paramref name="category"/>; empty for an unknown one.</summary>
    public static IReadOnlyList<AuditAction> ActionsOf(string category) =>
        [.. Groups.Where(entry => entry.Value == category).Select(static entry => entry.Key)];
}
