using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Billing.Revocation;

/// <summary>
/// Takes back the requests of a refunded or disputed pack: what is left of its grant by the
/// <see cref="ApiCreditFifo"/> rule comes off the balance of its key. Requests already spent
/// stay spent, and the balance never goes below zero.
/// </summary>
/// <remarks>
/// The grant is settled as the expiry settles one, dated and with the requests taken off, so
/// that the expiry job leaves it alone. The key's row is locked first, as the expiry job locks
/// it, so that the public API spends nothing in between. Its rate follows its credits: the
/// credits floor goes with the last of them.
/// </remarks>
internal sealed partial class CreditRevocation(
    LoDbDbContext db,
    BillingEffects effects,
    ILogger<CreditRevocation> logger)
{
    /// <param name="sessionId">The session that sold the pack.</param>
    /// <param name="at">When the payment was taken back, the date the grant is settled at.</param>
    /// <param name="cancellationToken">Cancels the writes, rolled back with the event.</param>
    public async Task RevokeAsync(
        string sessionId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (await LockKeyAsync(sessionId, cancellationToken) is not { } key)
        {
            // The buyer's account was gone: nothing was granted.
            LogNoGrant(logger, sessionId);
            return;
        }

        var live = await db.ApiCreditGrants
            .Where(grant => grant.ApiKeyId == key.Id && grant.ExpiredAt == null)
            .ToListAsync(cancellationToken);
        if (live.Find(grant => grant.StripeSessionId == sessionId) is not { } revoked)
        {
            LogAlreadySettled(logger, sessionId);
            return;
        }

        revoked.ExpiredRequests = ApiCreditFifo.RemainingOf(revoked, key.CreditsBalance, live);
        revoked.ExpiredAt = at;
        TakeBack(key, revoked);
    }

    // The key the grant of the session lies on, locked until the transaction ends; null when
    // the session has no grant.
    private async Task<ApiKey?> LockKeyAsync(string sessionId, CancellationToken cancellation)
    {
        var keyId = await db.ApiCreditGrants
            .Where(grant => grant.StripeSessionId == sessionId)
            .Select(static grant => (int?)grant.ApiKeyId)
            .FirstOrDefaultAsync(cancellation);
        return keyId is { } id
            ? await db.ApiKeys
                .FromSql($"SELECT * FROM api_keys WHERE id = {id} FOR UPDATE")
                .SingleAsync(cancellation)
            : null;
    }

    private void TakeBack(ApiKey key, ApiCreditGrant revoked)
    {
        var taken = revoked.ExpiredRequests ?? 0;
        key.CreditsBalance = Math.Max(0, key.CreditsBalance - taken);
        key.RateLimitPerMin = RateAfter(key);
        effects.KeyChanged(key);
        LogRevoked(logger, key.Id, taken, revoked.Requests);
    }

    // The rate of its plan, at the credits floor while credits are left; a plan the catalogue
    // does not know keeps the rate it has.
    private static int RateAfter(ApiKey key)
    {
        var planRate = key.Plan == ApiPlans.Free
            ? ApiPlans.FreeRate
            : ApiPlans.Find(key.Plan)?.RatePerMinute;
        return planRate is { } rate
            ? ApiPlans.RateOf(rate, key.CreditsBalance)
            : key.RateLimitPerMin;
    }

    [LoggerMessage(
        EventName = "billing.pack.revoked",
        Level = LogLevel.Warning,
        Message = "{Taken} of the {Requests} requests of a pack taken back from the API key "
            + "{ApiKeyId}.")]
    private static partial void LogRevoked(
        ILogger logger,
        int apiKeyId,
        long taken,
        long requests);

    [LoggerMessage(
        EventName = "billing.pack.no_grant",
        Level = LogLevel.Information,
        Message = "The pack session {SessionId} granted nothing: nothing to take back.")]
    private static partial void LogNoGrant(ILogger logger, string sessionId);

    [LoggerMessage(
        EventName = "billing.pack.already_settled",
        Level = LogLevel.Information,
        Message = "The grant of the pack session {SessionId} is already settled: nothing left "
            + "to take back.")]
    private static partial void LogAlreadySettled(ILogger logger, string sessionId);
}
