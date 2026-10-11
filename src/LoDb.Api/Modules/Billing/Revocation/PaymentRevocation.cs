using LoDb.Api.Modules.Billing.Payments;
using LoDb.Api.Modules.Billing.Subscriptions;
using LoDb.Infrastructure.Persistence.Billing.Payments;

namespace LoDb.Api.Modules.Billing.Revocation;

/// <summary>
/// Takes back what a refunded or disputed payment granted, as far as the site can: the
/// requests of a pack not spent yet, a donation and the supporter badge it gave, a plan.
/// </summary>
/// <remarks>
/// A payment names the session that sold it through <c>stripe_checkout</c>, which knows the
/// sessions paid since it exists. The payments of a subscription belong to its invoices,
/// whose customer names the key instead; a dispute names no customer, so a disputed renewal is
/// left to the owner, who ends the subscription. What the site cannot match is logged, never
/// guessed.
/// </remarks>
internal sealed partial class PaymentRevocation(
    CheckoutLedger ledger,
    CreditRevocation credits,
    DonationRevocation donations,
    SubscriptionEntitlements subscriptions,
    ILogger<PaymentRevocation> logger)
{
    public const string Refund = "refund";
    public const string Dispute = "dispute";

    public async Task RevokeAsync(PaymentEvent payment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);
        // A subscription's payments belong to its invoices: they are found by their customer.
        if (payment.PaymentIntentId is { Length: > 0 } paymentIntentId
            && await ledger.OfPaymentAsync(paymentIntentId, cancellationToken)
                is { Kind: not StripeCheckoutKind.Plan } checkout)
        {
            await RevokeCheckoutAsync(checkout, payment, cancellationToken);
            return;
        }

        var subscriptionId = payment.CustomerId is { Length: > 0 } customerId
            ? await subscriptions.OfCustomerAsync(customerId, cancellationToken)
            : null;
        if (subscriptionId is null)
        {
            LogUnmatched(logger, payment.Cause);
            return;
        }

        await subscriptions.SuspendAsync(
            new SubscriptionEvent(subscriptionId, payment.At),
            cancellationToken);
        LogPlanRevoked(logger, payment.Cause);
    }

    private async Task RevokeCheckoutAsync(
        StripeCheckout checkout,
        PaymentEvent payment,
        CancellationToken cancellationToken)
    {
        if (checkout.Status != StripeCheckoutStatus.Paid)
        {
            LogNotGranted(logger, checkout.Id, checkout.Status, payment.Cause);
            // Stripe does not deliver in order: the success of a payment taken back may still
            // be on its way, and must then grant nothing.
            if (checkout.Status == StripeCheckoutStatus.Pending)
            {
                Close(checkout, payment);
            }

            return;
        }

        await (checkout.Kind == StripeCheckoutKind.Donation
            ? donations.RevokeAsync(checkout.Id, cancellationToken)
            : credits.RevokeAsync(checkout.Id, payment.At, cancellationToken));
        Close(checkout, payment);
        LogRevoked(logger, checkout.Id, checkout.Kind, payment.Cause);
    }

    private static void Close(StripeCheckout checkout, PaymentEvent payment)
    {
        checkout.Status = StripeCheckoutStatus.Revoked;
        checkout.UpdatedAt = payment.At;
    }

    [LoggerMessage(
        EventName = "billing.payment.revoked",
        Level = LogLevel.Warning,
        Message = "The {Kind} session {SessionId} was taken back after a {Cause}.")]
    private static partial void LogRevoked(
        ILogger logger,
        string sessionId,
        StripeCheckoutKind kind,
        string cause);

    [LoggerMessage(
        EventName = "billing.payment.plan_revoked",
        Level = LogLevel.Warning,
        Message = "A subscription payment met a {Cause}: its key is on the free plan until "
            + "the subscription is paid up again.")]
    private static partial void LogPlanRevoked(ILogger logger, string cause);

    [LoggerMessage(
        EventName = "billing.payment.not_granted",
        Level = LogLevel.Information,
        Message = "The session {SessionId} met a {Cause} while {Status}: nothing to take back.")]
    private static partial void LogNotGranted(
        ILogger logger,
        string sessionId,
        StripeCheckoutStatus status,
        string cause);

    [LoggerMessage(
        EventName = "billing.payment.unmatched",
        Level = LogLevel.Warning,
        Message = "A {Cause} names no payment the site recorded: nothing taken back, to "
            + "settle by hand.")]
    private static partial void LogUnmatched(ILogger logger, string cause);
}
