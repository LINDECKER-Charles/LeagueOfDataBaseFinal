using Stripe;

namespace LoDb.Api.Modules.Billing.Webhooks;

/// <summary>The dates Stripe gives, as the columns store them.</summary>
internal static class StripeTimes
{
    /// <summary>
    /// When Stripe created <paramref name="stripeEvent"/>: for a completed session, when it
    /// was paid. Stripe.net reads it as a UTC date, whatever its kind says.
    /// </summary>
    public static DateTimeOffset CreatedAt(Event stripeEvent)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        return new DateTimeOffset(DateTime.SpecifyKind(stripeEvent.Created, DateTimeKind.Utc));
    }
}
