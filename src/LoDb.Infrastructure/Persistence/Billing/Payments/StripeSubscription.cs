namespace LoDb.Infrastructure.Persistence.Billing.Payments;

/// <summary>
/// A row of <c>stripe_subscription</c>: the plan a Stripe subscription pays for, and whether its
/// key holds it now.
/// </summary>
/// <remarks>
/// Stripe does not deliver its events in order: <see cref="ChangedAt"/> is the creation time of
/// the last event applied, and an older one changes nothing. The key of the subscription is the
/// active one whose <c>api_keys.stripe_subscription_id</c> names it.
/// </remarks>
public sealed class StripeSubscription
{
    /// <summary>Stripe's id of the subscription, such as <c>sub_1Q…</c>.</summary>
    public required string Id { get; set; }

    /// <summary>The plan it pays for, which its key gets back once it is paid up.</summary>
    public required string Plan { get; set; }

    public StripeSubscriptionStatus Status { get; set; }

    /// <summary>When Stripe created the last event applied to it.</summary>
    public DateTimeOffset ChangedAt { get; set; }
}
