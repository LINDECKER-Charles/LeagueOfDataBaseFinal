using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Webhooks;
using Stripe;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// <c>checkout.session.completed</c>, routed by the session's <c>kind</c>: a credit pack, a
/// subscription, or a donation, which a session without any kind is too.
/// </summary>
/// <remarks>
/// A kind the site does not know is left alone, where the legacy stack took it for a
/// donation: it is no session the site opened.
/// </remarks>
internal sealed partial class CheckoutCompletedHandler(
    PackFulfilment packs,
    PlanFulfilment plans,
    DonationFulfilment donations,
    ILogger<CheckoutCompletedHandler> logger) : IStripeEventHandler
{
    public string EventType => EventTypes.CheckoutSessionCompleted;

    public Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);
        if (stripeEvent.Data?.Object is not Session session)
        {
            LogNoSession(logger, stripeEvent.Id);
            return Task.CompletedTask;
        }

        var paidAt = StripeTimes.CreatedAt(stripeEvent);
        switch (SessionFields.Metadata(session, CheckoutMetadata.Kind))
        {
            case CheckoutMetadata.PackKind:
                return packs.ApplyAsync(session, paidAt, cancellationToken);
            case CheckoutMetadata.PlanKind:
                return plans.ApplyAsync(session, cancellationToken);
            case null or "" or CheckoutMetadata.DonationKind:
                return donations.RecordAsync(session, paidAt, cancellationToken);
            default:
                LogUnknownKind(logger, session.Id);
                return Task.CompletedTask;
        }
    }

    [LoggerMessage(
        EventName = "billing.checkout.no_session",
        Level = LogLevel.Warning,
        Message = "The completion event {EventId} carries no Checkout session: ignored.")]
    private static partial void LogNoSession(ILogger logger, string eventId);

    [LoggerMessage(
        EventName = "billing.checkout.unknown_kind",
        Level = LogLevel.Warning,
        Message = "The session {SessionId} is of a kind the site does not sell: ignored.")]
    private static partial void LogUnknownKind(ILogger logger, string sessionId);
}
