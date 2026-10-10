using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Billing.Revocation;

/// <summary>
/// Takes back a refunded or disputed donation: it leaves the donations, and its donor stays a
/// supporter only through another one.
/// </summary>
/// <remarks>
/// The row goes rather than being flagged, so that every total of the donations stays right;
/// <c>stripe_checkout</c> keeps the session, revoked, with its amount.
/// </remarks>
internal sealed partial class DonationRevocation(
    LoDbDbContext db,
    ILogger<DonationRevocation> logger)
{
    public async Task RevokeAsync(string sessionId, CancellationToken cancellationToken)
    {
        var donation = await db.Donations.FirstOrDefaultAsync(
            donation => donation.StripeSessionId == sessionId,
            cancellationToken);
        if (donation is null)
        {
            LogNotRecorded(logger, sessionId);
            return;
        }

        db.Donations.Remove(donation);
        if (donation.UserId is { } donorId
            && !await db.Donations.AnyAsync(
                other => other.UserId == donorId && other.Id != donation.Id,
                cancellationToken))
        {
            await db.Users
                .Where(user => user.Id == donorId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(user => user.IsSupporter, false),
                    cancellationToken);
        }

        LogRevoked(logger, sessionId, donation.AmountCents);
    }

    [LoggerMessage(
        EventName = "billing.donation.revoked",
        Level = LogLevel.Warning,
        Message = "The donation of {AmountCents} cents of the session {SessionId} was taken "
            + "back.")]
    private static partial void LogRevoked(ILogger logger, string sessionId, int amountCents);

    [LoggerMessage(
        EventName = "billing.donation.not_recorded",
        Level = LogLevel.Information,
        Message = "The donation session {SessionId} has no donation: nothing to take back.")]
    private static partial void LogNotRecorded(ILogger logger, string sessionId);
}
