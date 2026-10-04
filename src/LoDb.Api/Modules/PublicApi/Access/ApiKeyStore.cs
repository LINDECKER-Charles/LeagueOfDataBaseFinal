using LoDb.Api.Modules.PublicApi.Metering;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.PublicApi.Access;

/// <summary>
/// The reads and writes of <c>api_keys</c> a request of <c>/v1</c> makes: its key with the
/// requests of the month, and the spending of a prepaid credit.
/// </summary>
internal sealed class ApiKeyStore(
    IDbContextFactory<LoDbDbContext> contexts,
    UsageCalendar calendar)
{
    private long _sequence;

    /// <summary>The key whose fingerprint is <paramref name="hash"/>, null when none is.</summary>
    public async Task<ApiKeySnapshot?> LoadAsync(string hash, CancellationToken cancellationToken)
    {
        // Numbered before the read: a read that starts later sees every change committed
        // before it starts, so a greater number never carries older rights.
        var sequence = Interlocked.Increment(ref _sequence);
        var month = calendar.CurrentMonth;
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.ApiKeys
            .AsNoTracking()
            .Where(key => key.KeyHash == hash)
            .Select(key => new
            {
                key.Id,
                IsUsable = key.IsActive && key.RevokedAt == null,
                key.MonthlyQuota,
                key.CreditsBalance,
                key.RateLimitPerMin,
                Used = db.ApiUsage
                    .Where(usage => usage.ApiKeyId == key.Id && usage.Day >= month)
                    .Sum(usage => (long?)usage.Requests) ?? 0,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new ApiKeySnapshot(
                row.Id,
                row.IsUsable,
                row.MonthlyQuota,
                row.CreditsBalance,
                row.RateLimitPerMin,
                row.Used,
                month,
                sequence);
    }

    /// <summary>
    /// Takes one prepaid credit off the key, in the database itself so that concurrent
    /// requests never spend the same one.
    /// </summary>
    /// <returns>The balance left, or null when there was none to spend.</returns>
    public async Task<long?> SpendCreditAsync(int keyId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var balances = await db.Database
            .SqlQuery<long>(
                $"""
                UPDATE api_keys SET credits_balance = credits_balance - 1
                 WHERE id = {keyId} AND credits_balance > 0
                RETURNING credits_balance AS "Value"
                """)
            .ToListAsync(cancellationToken);
        return balances.Count == 0 ? null : balances[0];
    }
}
