using System.Globalization;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Modules.Profiles.Http;

/// <summary>
/// The audit events of the profile, shaped as the legacy stack shapes them: an update names
/// its section and no target, an erasure targets the account it removed.
/// </summary>
/// <remarks>
/// The journal reads the actor from the request, still signed in when it records.
/// </remarks>
internal sealed class ProfileAudit(IAuditLog log)
{
    public const string FavoritesSection = "favorites";

    /// <summary>
    /// A section of the rewrite: the legacy stack saved visibility with the favorites.
    /// </summary>
    public const string VisibilitySection = "visibility";

    public const string PreferredVersionSection = "preferred_version";
    public const string IdentitySection = "identity";
    public const string PasswordSection = "password";

    private const string SectionKey = "section";

    public Task UpdatedAsync(string section, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new AuditEvent
            {
                Action = AuditAction.ProfileUpdate,
                Meta = new Dictionary<string, object?> { [SectionKey] = section },
            },
            cancellationToken);

    /// <param name="account">The erased account, whose id and name the line keeps.</param>
    /// <param name="cancellationToken">Not observed by the journal.</param>
    public Task ErasedAsync(User account, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new AuditEvent
            {
                Action = AuditAction.AccountDelete,
                Target = new AuditTarget(
                    AuditTargetType.User,
                    account.Id.ToString(CultureInfo.InvariantCulture),
                    account.UserName),
            },
            cancellationToken);
}
