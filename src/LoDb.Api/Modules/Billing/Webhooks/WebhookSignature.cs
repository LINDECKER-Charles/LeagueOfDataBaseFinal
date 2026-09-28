using Stripe;

namespace LoDb.Api.Modules.Billing.Webhooks;

/// <summary>
/// Checks the <c>Stripe-Signature</c> of a payload, then reads its event, the way Stripe.net
/// does, against the service's clock.
/// </summary>
internal sealed class WebhookSignature(TimeProvider clock)
{
    /// <summary>Age a signature may have, Stripe's default: a replay older is refused.</summary>
    public const long ToleranceSeconds = 300;

    /// <summary>
    /// The event of <paramref name="payload"/>; null when the signature is missing, wrong or
    /// too old, or the payload is no event with an id and a type.
    /// </summary>
    /// <remarks>
    /// The API version of the event is not checked: only the fields the handlers read matter,
    /// and they are stable across versions.
    /// </remarks>
    public Event? Verify(string payload, string? header, string secret)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                payload,
                header ?? string.Empty,
                secret,
                ToleranceSeconds,
                clock.GetUtcNow().ToUnixTimeSeconds(),
                throwOnApiVersionMismatch: false);
            return stripeEvent is { Id.Length: > 0, Type.Length: > 0 } ? stripeEvent : null;
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            return null;
        }
    }

    // Stripe.net reads the payload with System.Text.Json, which throws InvalidOperationException
    // for JSON that is no object; Newtonsoft stays for its older paths.
    private static bool IsUnreadable(Exception exception) =>
        exception is StripeException
            or System.Text.Json.JsonException
            or InvalidOperationException
            or Newtonsoft.Json.JsonException;
}
