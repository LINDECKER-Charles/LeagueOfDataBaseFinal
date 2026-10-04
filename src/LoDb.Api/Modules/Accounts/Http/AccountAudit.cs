using System.Globalization;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Modules.Accounts.Http;

/// <summary>
/// The audit events of the accounts, shaped as the legacy stack shapes them: sign-in and
/// sign-out without a target, a failed sign-in anonymous with the identifier typed, the
/// other actions aimed at the account.
/// </summary>
/// <remarks>
/// The journal reads the actor from the request: the sign-in manager has set the signed-in
/// account by the time a sign-in is recorded, and a sign-out is recorded before the session
/// goes.
/// </remarks>
internal sealed class AccountAudit(IAuditLog log)
{
    /// <summary><c>method</c> of a sign-in with a password.</summary>
    public const string PasswordMethod = "password";

    /// <summary><c>method</c> of a sign-in through Google.</summary>
    public const string GoogleMethod = "google";

    private const string MethodKey = "method";
    private const string ChannelKey = "channel";
    private const string IdentifierKey = "identifier";
    private const string ReasonKey = "reason";

    // As long as the longest e-mail an account can have: anything longer names no account,
    // and the journal does not keep whatever a client sends.
    private const int MaxIdentifierLength = 180;

    public Task SignedInAsync(
        string method,
        SignInChannel channel,
        CancellationToken cancellationToken) =>
        log.RecordAsync(
            new AuditEvent
            {
                Action = AuditAction.UserLogin,
                Meta = new Dictionary<string, object?>
                {
                    [MethodKey] = method,
                    [ChannelKey] = channel.AuditName,
                },
            },
            cancellationToken);

    /// <param name="identifier">What was typed, or the e-mail Google gave.</param>
    /// <param name="reason">The code of the refusal answered to the client.</param>
    /// <param name="cancellationToken">Not observed by the journal.</param>
    public Task SignInFailedAsync(
        string? identifier,
        string reason,
        CancellationToken cancellationToken) =>
        log.RecordAsync(
            new AuditEvent
            {
                Action = AuditAction.UserLoginFailed,
                Outcome = AuditOutcome.Failure,
                Actor = AuditActor.Anonymous,
                Meta = new Dictionary<string, object?>
                {
                    [IdentifierKey] = Clip(identifier),
                    [ReasonKey] = reason,
                },
            },
            cancellationToken);

    public Task SignedOutAsync(CancellationToken cancellationToken) =>
        log.RecordAsync(new AuditEvent { Action = AuditAction.UserLogout }, cancellationToken);

    /// <summary>Records <paramref name="action"/> on <paramref name="account"/>.</summary>
    public Task RecordAsync(
        AuditAction action,
        User account,
        CancellationToken cancellationToken) =>
        log.RecordAsync(
            new AuditEvent
            {
                Action = action,
                Target = new AuditTarget(
                    AuditTargetType.User,
                    account.Id.ToString(CultureInfo.InvariantCulture),
                    account.UserName),
            },
            cancellationToken);

    private static string? Clip(string? identifier) =>
        identifier is { Length: > MaxIdentifierLength }
            ? identifier[..MaxIdentifierLength]
            : identifier;
}
