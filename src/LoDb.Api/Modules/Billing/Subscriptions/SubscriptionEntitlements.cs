using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Billing.Payments;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Billing.Subscriptions;

/// <summary>
/// The plan of the key a subscription pays for, as Stripe reports the subscription: held
/// while it is paid up, the free plan while it is not, back once it is, let go when it ends.
/// </summary>
/// <remarks>
/// Stripe does not deliver its events in order: each subscription keeps the creation time of
/// the last event applied, and an older event changes nothing. The key's row is locked first,
/// so that two events of one subscription apply one after the other. A subscription sold
/// before <c>stripe_subscription</c> existed gets its row from its key, which still holds the
/// plan it pays for, on the first event about it. Credits and their rate floor stay through
/// every change, and the key keeps its subscription until the subscription ends.
/// </remarks>
internal sealed partial class SubscriptionEntitlements(
    LoDbDbContext db,
    BillingEffects effects,
    ILogger<SubscriptionEntitlements> logger)
{
    /// <summary>
    /// Records that the subscription pays for <paramref name="planCode"/>, which a paid
    /// checkout just gave its key.
    /// </summary>
    public async Task EntitleAsync(
        SubscriptionEvent change,
        string planCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var row = await db.StripeSubscriptions.FindAsync([change.SubscriptionId], cancellationToken)
            ?? Track(change, planCode);
        row.Plan = planCode;
        row.Status = StripeSubscriptionStatus.Entitled;
        row.ChangedAt = Max(row.ChangedAt, change.At);
    }

    /// <summary>
    /// Puts the key of the subscription on the free plan, the subscription kept so that the
    /// plan comes back once it is paid up.
    /// </summary>
    public async Task SuspendAsync(SubscriptionEvent change, CancellationToken cancellationToken)
    {
        if (await HeldAsync(change, cancellationToken) is not { } held)
        {
            return;
        }

        held.Subscription.Status = StripeSubscriptionStatus.Suspended;
        Grant(held.Key, KeyRights.Free);
        LogSuspended(logger, held.Key.Id);
    }

    /// <summary>Gives the key of the subscription its plan back, once it is paid up.</summary>
    public async Task RestoreAsync(SubscriptionEvent change, CancellationToken cancellationToken)
    {
        if (await HeldAsync(change, cancellationToken) is not { } held)
        {
            return;
        }

        if (ApiPlans.Find(held.Subscription.Plan) is not { } plan)
        {
            LogPlanGone(logger, held.Key.Id, held.Subscription.Plan);
            return;
        }

        held.Subscription.Status = StripeSubscriptionStatus.Entitled;
        Grant(held.Key, KeyRights.Of(plan));
        LogRestored(logger, held.Key.Id, plan.Code);
    }

    /// <summary>
    /// Puts the key of the ended subscription on the free plan for good, and lets the
    /// subscription go. An end is final: no order of events undoes it.
    /// </summary>
    public async Task EndAsync(SubscriptionEvent change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (await LockKeyAsync(change.SubscriptionId, cancellationToken) is not { } key)
        {
            LogUnmatched(logger, change.SubscriptionId);
            return;
        }

        KeyRights.Free.GrantTo(key);
        key.StripeSubscriptionId = null;
        effects.KeyChanged(key);
        if (await db.StripeSubscriptions.FindAsync([change.SubscriptionId], cancellationToken)
            is { } row)
        {
            row.Status = StripeSubscriptionStatus.Ended;
            row.ChangedAt = Max(row.ChangedAt, change.At);
        }

        LogReleased(logger, key.Id);
    }

    /// <summary>
    /// The subscription of the newest active key of the Stripe customer; null when none of
    /// its keys holds one.
    /// </summary>
    public Task<string?> OfCustomerAsync(string customerId, CancellationToken cancellationToken) =>
        db.ApiKeys
            .Where(key => key.StripeCustomerId == customerId
                && key.IsActive
                && key.StripeSubscriptionId != null)
            .OrderByDescending(key => key.Id)
            .Select(key => key.StripeSubscriptionId)
            .FirstOrDefaultAsync(cancellationToken);

    // The key and the row of the subscription, the event being the newest applied; null when
    // there is nothing to change.
    private async Task<Held?> HeldAsync(SubscriptionEvent change, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (await LockKeyAsync(change.SubscriptionId, cancellation) is not { } key)
        {
            LogUnmatched(logger, change.SubscriptionId);
            return null;
        }

        var row = await db.StripeSubscriptions.FindAsync([change.SubscriptionId], cancellation)
            ?? Adopt(key, change);
        if (row is null || change.At < row.ChangedAt)
        {
            LogUnchanged(logger, key.Id, change.At);
            return null;
        }

        row.ChangedAt = change.At;
        return new Held(key, row);
    }

    // Until the transaction ends: the public API, the expiry job and the other events of the
    // subscription wait for it.
    private Task<ApiKey?> LockKeyAsync(string subscriptionId, CancellationToken cancellation) =>
        db.ApiKeys
            .FromSql(
                $"""
                SELECT * FROM api_keys
                WHERE stripe_subscription_id = {subscriptionId} AND is_active
                ORDER BY id DESC LIMIT 1
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(cancellation);

    // A subscription sold before the table existed: its key still holds the plan it pays for,
    // unless that is no plan on sale, which leaves nothing to restore.
    private StripeSubscription? Adopt(ApiKey key, SubscriptionEvent change) =>
        ApiPlans.Find(key.Plan) is { } plan ? Track(change, plan.Code) : null;

    private StripeSubscription Track(SubscriptionEvent change, string planCode)
    {
        var row = new StripeSubscription
        {
            Id = change.SubscriptionId,
            Plan = planCode,
            Status = StripeSubscriptionStatus.Entitled,
            ChangedAt = change.At,
        };
        db.StripeSubscriptions.Add(row);
        return row;
    }

    private void Grant(ApiKey key, KeyRights rights)
    {
        if (!rights.AreHeldBy(key))
        {
            rights.GrantTo(key);
            effects.KeyChanged(key);
        }
    }

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) =>
        first > second ? first : second;

    private sealed record Held(ApiKey Key, StripeSubscription Subscription);

    [LoggerMessage(
        EventName = "billing.plan.suspended",
        Level = LogLevel.Information,
        Message = "The subscription of the API key {ApiKeyId} lapsed: free plan until it is "
            + "paid up.")]
    private static partial void LogSuspended(ILogger logger, int apiKeyId);

    [LoggerMessage(
        EventName = "billing.plan.restored",
        Level = LogLevel.Information,
        Message = "The subscription of the API key {ApiKeyId} is paid up: plan {Plan} back.")]
    private static partial void LogRestored(ILogger logger, int apiKeyId, string plan);

    [LoggerMessage(
        EventName = "billing.plan.released",
        Level = LogLevel.Information,
        Message = "The subscription of the API key {ApiKeyId} ended: back to the free plan.")]
    private static partial void LogReleased(ILogger logger, int apiKeyId);

    [LoggerMessage(
        EventName = "billing.plan.unmatched",
        Level = LogLevel.Information,
        Message = "The subscription {SubscriptionId} matches no active key: nothing changed.")]
    private static partial void LogUnmatched(ILogger logger, string subscriptionId);

    [LoggerMessage(
        EventName = "billing.plan.unchanged",
        Level = LogLevel.Information,
        Message = "An event of {EventAt} about the subscription of the API key {ApiKeyId} is "
            + "older than the last one applied, or names no plan on sale: nothing changed.")]
    private static partial void LogUnchanged(ILogger logger, int apiKeyId, DateTimeOffset eventAt);

    [LoggerMessage(
        EventName = "billing.plan.gone",
        Level = LogLevel.Warning,
        Message = "The subscription of the API key {ApiKeyId} pays for the plan {Plan}, no "
            + "longer on sale: its plan is not restored.")]
    private static partial void LogPlanGone(ILogger logger, int apiKeyId, string plan);
}
