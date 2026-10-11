using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Payments;

/// <summary>Stripe's <c>payment_status</c> of a completed Checkout session.</summary>
internal static class SessionPayments
{
    public const string Paid = "paid";

    /// <summary>Nothing to charge, as for a session discounted to zero.</summary>
    public const string NoPaymentRequired = "no_payment_required";

    /// <summary>
    /// Whether the money of <paramref name="session"/> is in. A delayed payment method
    /// completes the session <c>unpaid</c>, and the asynchronous events settle it later.
    /// </summary>
    public static bool IsSettled(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.PaymentStatus is Paid or NoPaymentRequired;
    }
}
