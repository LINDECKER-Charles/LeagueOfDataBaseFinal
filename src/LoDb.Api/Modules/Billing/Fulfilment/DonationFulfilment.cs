using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Billing;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// A completed donation (<c>kind: donation</c>, or no kind at all): kept once per session,
/// its signed-in donor becoming a supporter, as the legacy <c>DonationRecorder</c> does.
/// </summary>
internal sealed partial class DonationFulfilment(
    LoDbDbContext db,
    ILogger<DonationFulfilment> logger)
{
    /// <param name="session">The completed session.</param>
    /// <param name="paidAt">When it was paid, the date of the donation.</param>
    /// <param name="cancellationToken">Cancels the writes, rolled back with the event.</param>
    public async Task RecordAsync(
        Session session,
        DateTimeOffset paidAt,
        CancellationToken cancellationToken)
    {
        if (await db.Donations.AnyAsync(
                donation => donation.StripeSessionId == session.Id,
                cancellationToken))
        {
            LogAlreadyRecorded(logger, session.Id);
            return;
        }

        var donorId = await DonorAsync(SessionFields.DonorId(session), cancellationToken);
        var donation = new Donation
        {
            StripeSessionId = session.Id,
            AmountCents = (int)Math.Clamp(session.AmountTotal ?? 0, 0, int.MaxValue),
            Currency = session.Currency ?? string.Empty,
            CreatedAt = paidAt,
            UserId = donorId,
        };
        db.Donations.Add(donation);
        if (donorId is { } id)
        {
            await db.Users
                .Where(user => user.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(user => user.IsSupporter, true),
                    cancellationToken);
        }

        LogRecorded(logger, session.Id, donation.AmountCents, donorId is not null);
    }

    // A donor whose account is gone gives anonymously.
    private async Task<int?> DonorAsync(int? donorId, CancellationToken cancellationToken) =>
        donorId is { } id
        && await db.Users.AnyAsync(user => user.Id == id, cancellationToken)
            ? id
            : null;

    [LoggerMessage(
        EventName = "billing.donation.recorded",
        Level = LogLevel.Information,
        Message = "Donation of {AmountCents} cents recorded for the session {SessionId} "
            + "(signed-in donor: {SignedIn}).")]
    private static partial void LogRecorded(
        ILogger logger,
        string sessionId,
        int amountCents,
        bool signedIn);

    [LoggerMessage(
        EventName = "billing.donation.already_recorded",
        Level = LogLevel.Information,
        Message = "The donation session {SessionId} is already recorded: not recorded again.")]
    private static partial void LogAlreadyRecorded(ILogger logger, string sessionId);
}
