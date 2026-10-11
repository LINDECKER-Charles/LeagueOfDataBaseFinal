using LoDb.Api.Modules.Billing.Payments;
using LoDb.Api.Modules.Billing.Webhooks;
using LoDb.Infrastructure.Persistence.Billing.Payments;
using Stripe;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// <c>checkout.session.async_payment_failed</c>: the delayed payment of a completed session
/// failed. Nothing was granted for it, and the session is recorded as failed.
/// </summary>
internal sealed partial class AsyncPaymentFailedHandler(
    SessionEventReader reader,
    CheckoutLedger ledger,
    ILogger<AsyncPaymentFailedHandler> logger) : IStripeEventHandler
{
    public string EventType => EventTypes.CheckoutSessionAsyncPaymentFailed;

    public async Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (reader.Read(stripeEvent) is not { } failed)
        {
            return;
        }

        var sessionId = failed.Session.Id;
        if (await ledger.StatusOfAsync(sessionId, cancellationToken) is { } status
            && status != StripeCheckoutStatus.Pending)
        {
            LogSettled(logger, sessionId, status);
            return;
        }

        await ledger.RecordAsync(failed, StripeCheckoutStatus.Failed, cancellationToken);
        LogFailed(logger, sessionId);
    }

    [LoggerMessage(
        EventName = "billing.checkout.payment_failed",
        Level = LogLevel.Information,
        Message = "The delayed payment of the session {SessionId} failed: nothing granted.")]
    private static partial void LogFailed(ILogger logger, string sessionId);

    [LoggerMessage(
        EventName = "billing.checkout.failure_ignored",
        Level = LogLevel.Information,
        Message = "The session {SessionId} is already {Status}: its failed payment changes "
            + "nothing.")]
    private static partial void LogSettled(
        ILogger logger,
        string sessionId,
        StripeCheckoutStatus status);
}
