using LoDb.Api.Modules.Billing.Keys;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Billing.Expiry;

/// <summary>
/// Settles the credits twelve months old: what is left of a due grant, by
/// <see cref="ApiCreditFifo"/>, comes off the balance of its key.
/// </summary>
/// <remarks>
/// An active key whose balance its live grants do not cover, such as credits the legacy
/// stack sold meanwhile, first gets a reconciliation grant for the difference, dated today,
/// so that it expires too. Each key is settled in its own transaction, its row locked, so
/// that the public API spends nothing in between; its rate does not change, as in the legacy
/// stack. The logs name keys by id only.
/// </remarks>
internal sealed partial class CreditExpiry(
    LoDbDbContext db,
    BillingEffects effects,
    TimeProvider clock,
    ILogger<CreditExpiry> logger)
{
    /// <summary>Settles every key that needs it; returns how many were.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var settled = 0;
        foreach (var keyId in await DueKeysAsync(now, cancellationToken))
        {
            if (await SettleAsync(keyId, now, cancellationToken))
            {
                settled += 1;
            }
        }

        return settled;
    }

    // Keys with a live grant due, or active keys with a balance their live grants miss.
    private Task<List<int>> DueKeysAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var live = db.ApiCreditGrants.Where(grant => grant.ExpiredAt == null);
        return db.ApiKeys
            .Where(key =>
                live.Any(grant => grant.ApiKeyId == key.Id && grant.ExpiresAt <= now)
                || (key.IsActive
                    && key.CreditsBalance > live
                        .Where(grant => grant.ApiKeyId == key.Id)
                        .Sum(grant => grant.Requests)))
            .OrderBy(key => key.Id)
            .Select(key => key.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> SettleAsync(
        int keyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var key = await db.ApiKeys
            .FromSql($"SELECT * FROM api_keys WHERE id = {keyId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (key is null)
        {
            return false;
        }

        var live = await db.ApiCreditGrants
            .Where(grant => grant.ApiKeyId == keyId && grant.ExpiredAt == null)
            .ToListAsync(cancellationToken);
        var reconciled = Reconcile(key, live, now);
        var expired = Expire(key, live, now);
        key.CreditsBalance = Math.Max(0, key.CreditsBalance - expired);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        if (expired > 0)
        {
            effects.KeyChanged(key);
            await effects.ApplyAsync();
        }

        LogSettled(logger, keyId, expired, reconciled);
        return true;
    }

    // Returns the requests granted to cover the balance.
    private long Reconcile(ApiKey key, List<ApiCreditGrant> live, DateTimeOffset now)
    {
        var uncovered = ApiCreditFifo.Uncovered(key.CreditsBalance, live);
        if (!key.IsActive || uncovered == 0)
        {
            return 0;
        }

        var grant = new ApiCreditGrant
        {
            ApiKeyId = key.Id,
            Source = ApiCreditGrantSource.Reconciliation,
            Requests = uncovered,
            PurchasedAt = now,
            ExpiresAt = now.AddMonths(ApiCreditGrant.ValidityMonths),
        };
        db.ApiCreditGrants.Add(grant);
        live.Add(grant);
        return uncovered;
    }

    // What is left of each due grant is measured against the same balance and grants before
    // any is settled; returns the requests to take off.
    private static long Expire(ApiKey key, List<ApiCreditGrant> live, DateTimeOffset now)
    {
        var shares = live
            .Where(grant => grant.ExpiresAt <= now)
            .Select(grant => (Grant: grant, Left: ApiCreditFifo.RemainingOf(
                grant,
                key.CreditsBalance,
                live)))
            .ToList();
        foreach (var (grant, left) in shares)
        {
            grant.ExpiredAt = now;
            grant.ExpiredRequests = left;
        }

        return shares.Sum(static share => share.Left);
    }

    [LoggerMessage(
        EventName = "billing.credits.expired",
        Level = LogLevel.Information,
        Message = "API key {ApiKeyId}: {Expired} requests expired, {Reconciled} reconciled.")]
    private static partial void LogSettled(
        ILogger logger,
        int apiKeyId,
        long expired,
        long reconciled);
}
