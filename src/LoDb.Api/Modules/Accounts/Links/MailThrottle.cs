using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>
/// Counts the e-mails each account asked for, in <c>identity_user_tokens</c>: every instance
/// sees the same counts, and they go with the account.
/// </summary>
/// <remarks>
/// Two requests at the same instant may both pass: the limit is there against a mailbox
/// flooded by a script, which one e-mail more does not change.
/// </remarks>
internal sealed class MailThrottle(LoDbDbContext db, TimeProvider clock)
{
    // Identity's own rows use "[AspNetUserStore]".
    private const string LoginProvider = "LoDb";

    /// <summary>
    /// How long before <paramref name="rule"/> lets another e-mail go; null if it does now.
    /// </summary>
    public async Task<TimeSpan?> RemainingWaitAsync(
        int userId,
        MailThrottleRule rule,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var tally = Tally(await FindAsync(userId, rule, cancellationToken), rule);
        if (tally.Total < rule.Allowance || tally.Recent.Count < rule.Limit)
        {
            return null;
        }

        return tally.Recent[tally.Recent.Count - rule.Limit] + rule.Window - clock.GetUtcNow();
    }

    /// <summary>Counts an e-mail sent now, written by the caller's next save.</summary>
    public async Task RecordAsync(
        int userId,
        MailThrottleRule rule,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var row = await FindAsync(userId, rule, cancellationToken);
        var value = Tally(row, rule).Add(clock.GetUtcNow()).Format();
        if (row is not null)
        {
            row.Value = value;
            return;
        }

        db.Set<IdentityUserToken<int>>().Add(new IdentityUserToken<int>
        {
            UserId = userId,
            LoginProvider = LoginProvider,
            Name = rule.Name,
            Value = value,
        });
    }

    private async Task<IdentityUserToken<int>?> FindAsync(
        int userId,
        MailThrottleRule rule,
        CancellationToken cancellationToken) =>
        await db.Set<IdentityUserToken<int>>()
            .FindAsync([userId, LoginProvider, rule.Name], cancellationToken);

    private MailTally Tally(IdentityUserToken<int>? row, MailThrottleRule rule) =>
        MailTally.Parse(row?.Value, clock.GetUtcNow() - rule.Window);
}
