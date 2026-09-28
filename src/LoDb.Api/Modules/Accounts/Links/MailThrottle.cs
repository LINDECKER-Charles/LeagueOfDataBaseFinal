using System.Globalization;
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
    private const char Separator = ' ';

    /// <summary>
    /// How long before <paramref name="rule"/> lets another e-mail go; null if it does now.
    /// </summary>
    public async Task<TimeSpan?> RemainingWaitAsync(
        int userId,
        MailThrottleRule rule,
        CancellationToken cancellationToken)
    {
        var sent = Recent(await FindAsync(userId, rule, cancellationToken), rule);
        return sent.Count < rule.Limit
            ? null
            : sent[sent.Count - rule.Limit] + rule.Window - clock.GetUtcNow();
    }

    /// <summary>Counts an e-mail sent now, written by the caller's next save.</summary>
    public async Task RecordAsync(
        int userId,
        MailThrottleRule rule,
        CancellationToken cancellationToken)
    {
        var row = await FindAsync(userId, rule, cancellationToken);
        var value = string.Join(
            Separator,
            Recent(row, rule).Append(clock.GetUtcNow()).Select(static sent =>
                sent.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)));
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

    // Oldest first, those still within the window.
    private List<DateTimeOffset> Recent(IdentityUserToken<int>? row, MailThrottleRule rule)
    {
        var since = clock.GetUtcNow() - rule.Window;
        return [.. (row?.Value ?? string.Empty)
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => long.TryParse(
                part,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var milliseconds)
                ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
                : DateTimeOffset.MinValue)
            .Where(sent => sent > since)
            .Order()];
    }
}
