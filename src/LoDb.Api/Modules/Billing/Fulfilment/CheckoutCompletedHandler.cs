using LoDb.Api.Modules.Billing.Payments;
using LoDb.Api.Modules.Billing.Webhooks;
using LoDb.Infrastructure.Persistence.Billing.Payments;
using Stripe;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// <c>checkout.session.completed</c>: a session paid at once is granted, as its <c>kind</c>
/// says; one whose payment is still on its way, by a delayed method, is recorded as pending
/// and granted only once <c>checkout.session.async_payment_succeeded</c> arrives.
/// </summary>
internal sealed partial class CheckoutCompletedHandler(
    SessionEventReader reader,
    SessionFulfilment fulfilment,
    CheckoutLedger ledger,
    ILogger<CheckoutCompletedHandler> logger) : IStripeEventHandler
{
    public string EventType => EventTypes.CheckoutSessionCompleted;

    public async Task HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (reader.Read(stripeEvent) is not { } completed)
        {
            return;
        }

        if (SessionPayments.IsSettled(completed.Session))
        {
            await fulfilment.FulfilAsync(completed, cancellationToken);
            return;
        }

        // On first sight only: the success of its payment may have come in first.
        if (await ledger.StatusOfAsync(completed.Session.Id, cancellationToken) is null)
        {
            await ledger.RecordAsync(completed, StripeCheckoutStatus.Pending, cancellationToken);
        }

        LogPending(logger, completed.Session.Id, completed.Session.PaymentStatus);
    }

    [LoggerMessage(
        EventName = "billing.checkout.pending",
        Level = LogLevel.Information,
        Message = "The session {SessionId} completed {PaymentStatus}: nothing granted until "
            + "its payment comes in.")]
    private static partial void LogPending(ILogger logger, string sessionId, string? paymentStatus);
}
