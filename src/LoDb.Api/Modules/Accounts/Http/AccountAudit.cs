using System.Globalization;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Accounts;

namespace LoDb.Api.Modules.Accounts.Http;

/// <summary>
/// The audit events of the accounts, shaped as the legacy stack shapes them: sign-in and
/// sign-out without a target, a failed sign-in anonymous with what stands for the identifier
/// typed (<see cref="IdentifierDigest"/>), the other actions aimed at the account.
/// </summary>
/// <remarks>
/// The journal reads the actor from the request: the sign-in manager has set the signed-in
/// account by the time a sign-in is recorded, and a sign-out is recorded before the session
/// goes.
/// </remarks>
internal sealed class AccountAudit(IAuditLog log, IdentifierDigest identifiers)
{
    /// <summary><c>method</c> of a sign-in with a password.</summary>
    public const string PasswordMethod = "password";

    /// <summary><c>method</c> of a sign-in through Google.</summary>
    public const string GoogleMethod = "google";

    private const string MethodKey = "method";
    private const string ChannelKey = "channel";
    private const string IdentifierKindKey = "identifier_kind";
    private const string IdentifierDigestKey = "identifier_digest";
    private const string ReasonKey = "reason";
    private const string ScopeKey = "scope";
    private const string EverywhereScope = "everywhere";

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

    /// <param name="identifier">
    /// What was typed, or the e-mail Google gave: journaled as its kind and digest only.
    /// </param>
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
                Meta = FailureMeta(identifier, reason),
            },
            cancellationToken);

    public Task SignedOutAsync(CancellationToken cancellationToken) =>
        log.RecordAsync(new AuditEvent { Action = AuditAction.UserLogout }, cancellationToken);

    /// <summary>A sign-out of every session of the account (<c>scope</c>: everywhere).</summary>
    public Task SignedOutEverywhereAsync(CancellationToken cancellationToken) =>
        log.RecordAsync(
            new AuditEvent
            {
                Action = AuditAction.UserLogout,
                Meta = new Dictionary<string, object?> { [ScopeKey] = EverywhereScope },
            },
            cancellationToken);

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

    // Nothing of an input that names no account in any shape: it may be anything typed.
    private Dictionary<string, object?> FailureMeta(string? identifier, string reason)
    {
        var meta = new Dictionary<string, object?> { [ReasonKey] = reason };
        if (IdentifierDigest.KindOf(identifier) is { } kind)
        {
            meta[IdentifierKindKey] = kind;
            meta[IdentifierDigestKey] = identifiers.Of(identifier!);
        }

        return meta;
    }
}
