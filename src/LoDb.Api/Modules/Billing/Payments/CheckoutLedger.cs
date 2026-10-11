using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Billing.Payments;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Billing.Payments;

/// <summary>
/// The rows of <c>stripe_checkout</c>: what became of each session the webhook received.
/// </summary>
/// <remarks>
/// Written through the scoped context, saved with the row of the event: a handler that fails
/// leaves none. Two events of one unseen session applied at the same time both add it: the
/// primary key fails the second, which rolls back and finds the row on its next delivery.
/// </remarks>
internal sealed class CheckoutLedger(LoDbDbContext db)
{
    /// <summary>The status of the session; null when the webhook never recorded it.</summary>
    public async Task<StripeCheckoutStatus?> StatusOfAsync(
        string sessionId,
        CancellationToken cancellationToken) =>
        (await db.StripeCheckouts.FindAsync([sessionId], cancellationToken))?.Status;

    /// <summary>
    /// Sets the session of <paramref name="change"/> to <paramref name="status"/>, adding its
    /// row on first sight.
    /// </summary>
    public async Task RecordAsync(
        SessionEvent change,
        StripeCheckoutStatus status,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var row = await db.StripeCheckouts.FindAsync([change.Session.Id], cancellationToken);
        if (row is null)
        {
            row = Open(change);
            db.StripeCheckouts.Add(row);
        }

        row.Status = status;
        row.UpdatedAt = change.At;
        row.PaymentIntentId ??= PaymentOf(change);
    }

    /// <summary>The session sold by the payment; null when no recorded session names it.</summary>
    public Task<StripeCheckout?> OfPaymentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken) =>
        db.StripeCheckouts.FirstOrDefaultAsync(
            checkout => checkout.PaymentIntentId == paymentIntentId,
            cancellationToken);

    private static StripeCheckout Open(SessionEvent change) => new()
    {
        Id = change.Session.Id,
        Kind = change.Kind,
        AmountCents = change.Session.AmountTotal ?? 0,
        Currency = change.Session.Currency ?? string.Empty,
        CreatedAt = change.At,
    };

    private static string? PaymentOf(SessionEvent change) =>
        string.IsNullOrEmpty(change.Session.PaymentIntentId)
            ? null
            : change.Session.PaymentIntentId;
}
