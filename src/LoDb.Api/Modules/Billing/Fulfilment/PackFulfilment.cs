using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// A paid credit pack (<c>kind: api_pack</c>): its requests join the balance of the buyer's
/// key, whose rate rises to the credits floor, and a grant dates them for their expiry.
/// </summary>
/// <remarks>
/// The legacy stack credited a session again on each redelivery: the event's row prevents it
/// now, and a grant already made for the session too, as for a pack the legacy stack sold.
/// Two events of one session applied at the same time both miss the grant: the unique index
/// on its session fails the second, which rolls back and finds it on its next delivery.
/// </remarks>
internal sealed partial class PackFulfilment(
    LoDbDbContext db,
    BuyerKeys keys,
    BillingEffects effects,
    ILogger<PackFulfilment> logger)
{
    /// <param name="session">The completed session.</param>
    /// <param name="paidAt">When it was paid, from which the grant runs twelve months.</param>
    /// <param name="cancellationToken">Cancels the writes, rolled back with the event.</param>
    public async Task ApplyAsync(
        Session session,
        DateTimeOffset paidAt,
        CancellationToken cancellationToken)
    {
        var requests = SessionFields.Requests(session);
        if (requests <= 0)
        {
            LogWithoutRequests(logger, session.Id);
            return;
        }

        if (await db.ApiCreditGrants.AnyAsync(
                grant => grant.StripeSessionId == session.Id,
                cancellationToken))
        {
            LogAlreadyGranted(logger, session.Id);
            return;
        }

        if (await keys.ActiveKeyAsync(SessionFields.BuyerId(session), cancellationToken)
            is not { } key)
        {
            return;
        }

        await CreditAsync(key, requests, cancellationToken);
        db.ApiCreditGrants.Add(Grant(key, session, paidAt));
        effects.KeyChanged(key);
        LogCredited(logger, key.Id, requests);
    }

    private Task<int> CreditAsync(ApiKey key, long requests, CancellationToken cancellationToken) =>
        db.ApiKeys
            .Where(row => row.Id == key.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.CreditsBalance, row => row.CreditsBalance + requests)
                    .SetProperty(
                        row => row.RateLimitPerMin,
                        row => Math.Max(row.RateLimitPerMin, ApiPlans.CreditsRate)),
                cancellationToken);

    private static ApiCreditGrant Grant(ApiKey key, Session session, DateTimeOffset paidAt) => new()
    {
        ApiKeyId = key.Id,
        Source = ApiCreditGrantSource.Purchase,
        Requests = SessionFields.Requests(session),
        PurchasedAt = paidAt,
        ExpiresAt = paidAt.AddMonths(ApiCreditGrant.ValidityMonths),
        StripeSessionId = session.Id,
    };

    [LoggerMessage(
        EventName = "billing.pack.credited",
        Level = LogLevel.Information,
        Message = "{Requests} requests credited to the API key {ApiKeyId}.")]
    private static partial void LogCredited(ILogger logger, int apiKeyId, long requests);

    [LoggerMessage(
        EventName = "billing.pack.without_requests",
        Level = LogLevel.Warning,
        Message = "The pack session {SessionId} carries no requests: nothing credited.")]
    private static partial void LogWithoutRequests(ILogger logger, string sessionId);

    [LoggerMessage(
        EventName = "billing.pack.already_granted",
        Level = LogLevel.Information,
        Message = "The pack session {SessionId} was already credited: not credited again.")]
    private static partial void LogAlreadyGranted(ILogger logger, string sessionId);
}
