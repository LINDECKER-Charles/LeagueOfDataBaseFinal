using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Accounts.Links;

/// <summary>
/// Counts the e-mails each account asked for, in <c>identity_user_tokens</c>: every instance
/// sees the same counts, and they go with the account.
/// </summary>
/// <remarks>
/// A caller reads the count and records its e-mail within one transaction, after
/// <see cref="HoldAsync"/>: requests of one account sent at once would otherwise all read
/// the same count and all pass, as many e-mails as requests.
/// </remarks>
internal sealed class MailThrottle(LoDbDbContext db, TimeProvider clock)
{
    // Identity's own rows use "[AspNetUserStore]".
    private const string LoginProvider = "LoDb";

    /// <summary>
    /// Locks the account's row until the caller's transaction ends: the next request of the
    /// account reads the count once this one has recorded its e-mail, or given up.
    /// </summary>
    public async Task HoldAsync(int userId, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM users WHERE id = {userId} FOR UPDATE",
            cancellationToken);

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
