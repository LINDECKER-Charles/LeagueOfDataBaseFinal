using LoDb.Api.Modules.Billing.Webhooks;
using Stripe;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Payments;

/// <summary>
/// Reads the session of a <c>checkout.session.*</c> event, logging what it skips.
/// </summary>
internal sealed partial class SessionEventReader(ILogger<SessionEventReader> logger)
{
    /// <summary>
    /// The session of <paramref name="stripeEvent"/>; null when it carries none, or one of a
    /// kind the site does not sell, which is acknowledged and left alone.
    /// </summary>
    public SessionEvent? Read(Event stripeEvent)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        if (stripeEvent.Data?.Object is not Session session)
        {
            LogNoSession(logger, stripeEvent.Id, stripeEvent.Type);
            return null;
        }

        if (CheckoutKinds.Of(session) is not { } kind)
        {
            LogUnknownKind(logger, session.Id);
            return null;
        }

        return new SessionEvent(session, kind, StripeTimes.CreatedAt(stripeEvent));
    }

    [LoggerMessage(
        EventName = "billing.checkout.no_session",
        Level = LogLevel.Warning,
        Message = "The event {EventId} of type {EventType} carries no Checkout session: ignored.")]
    private static partial void LogNoSession(ILogger logger, string eventId, string eventType);

    [LoggerMessage(
        EventName = "billing.checkout.unknown_kind",
        Level = LogLevel.Warning,
        Message = "The session {SessionId} is of a kind the site does not sell: ignored.")]
    private static partial void LogUnknownKind(ILogger logger, string sessionId);
}
