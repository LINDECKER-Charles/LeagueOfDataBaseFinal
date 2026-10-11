using LoDb.Api.Modules.Billing.Payments;
using LoDb.Infrastructure.Persistence.Billing.Payments;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// Grants a paid session once, at the catalogue's price only: a credit pack, a subscription or
/// a donation.
/// </summary>
/// <remarks>
/// A session paid at once (<c>checkout.session.completed</c>) and a delayed payment that came
/// in (<c>checkout.session.async_payment_succeeded</c>) both come here. A session already
/// settled, paid, failed, refused or revoked, is left alone, whichever event names it again.
/// </remarks>
internal sealed partial class SessionFulfilment(
    CheckoutLedger ledger,
    PackFulfilment packs,
    PlanFulfilment plans,
    DonationFulfilment donations,
    ILogger<SessionFulfilment> logger)
{
    public async Task FulfilAsync(SessionEvent paid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paid);
        var session = paid.Session;
        if (await ledger.StatusOfAsync(session.Id, cancellationToken) is { } status
            && status != StripeCheckoutStatus.Pending)
        {
            LogSettled(logger, session.Id, status);
            return;
        }

        if (!CataloguePrices.Match(paid))
        {
            await ledger.RecordAsync(paid, StripeCheckoutStatus.Refused, cancellationToken);
            LogRefused(logger, session.Id, paid.Kind, session.AmountTotal, session.Currency);
            return;
        }

        await ledger.RecordAsync(paid, StripeCheckoutStatus.Paid, cancellationToken);
        await GrantAsync(paid, cancellationToken);
    }

    private Task GrantAsync(SessionEvent paid, CancellationToken cancellationToken) =>
        paid.Kind switch
        {
            StripeCheckoutKind.Pack => packs.ApplyAsync(paid.Session, paid.At, cancellationToken),
            StripeCheckoutKind.Plan => plans.ApplyAsync(paid.Session, paid.At, cancellationToken),
            _ => donations.RecordAsync(paid.Session, paid.At, cancellationToken),
        };

    [LoggerMessage(
        EventName = "billing.checkout.settled",
        Level = LogLevel.Information,
        Message = "The session {SessionId} is already {Status}: nothing granted again.")]
    private static partial void LogSettled(
        ILogger logger,
        string sessionId,
        StripeCheckoutStatus status);

    [LoggerMessage(
        EventName = "billing.checkout.refused",
        Level = LogLevel.Warning,
        Message = "The {Kind} session {SessionId} charged {AmountCents} {Currency}, not the "
            + "catalogue's price: nothing granted, to refund or settle by hand.")]
    private static partial void LogRefused(
        ILogger logger,
        string sessionId,
        StripeCheckoutKind kind,
        long? amountCents,
        string? currency);
}
