namespace LoDb.Infrastructure.Persistence.Billing;

/// <summary>
/// A row of <c>stripe_event</c>: a webhook event already handled, which a redelivery must
/// not apply again.
/// </summary>
/// <remarks>
/// The row is inserted in the transaction of the effects of the event: a conflict on
/// <see cref="Id"/> means a duplicate, and a failed handler leaves no row, so that Stripe's
/// next delivery runs it again.
/// </remarks>
public sealed class StripeEvent
{
    /// <summary>Stripe's id of the event, such as <c>evt_1Q…</c>.</summary>
    public required string Id { get; set; }

    /// <summary>Stripe's type of the event, such as <c>checkout.session.completed</c>.</summary>
    public required string Type { get; set; }

    public StripeEventStatus Status { get; set; }

    /// <summary>When Stripe created the event (<c>created</c>).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
