namespace LoDb.Api.Modules.Billing.Subscriptions;

/// <summary>One of Stripe's events about a subscription.</summary>
/// <param name="SubscriptionId">Stripe's id of the subscription, such as <c>sub_1Q…</c>.</param>
/// <param name="At">
/// When Stripe created the event, which orders the events of one subscription.
/// </param>
internal sealed record SubscriptionEvent(string SubscriptionId, DateTimeOffset At);
