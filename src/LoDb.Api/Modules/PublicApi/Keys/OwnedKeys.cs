using System.Globalization;
using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.PublicApi.Keys;

/// <summary>
/// The changes an account makes to its own key: one active key at most, whose rights
/// outlive its secret.
/// </summary>
/// <remarks>
/// Each change locks the account's row until it commits, as a payment issuing a key does
/// (<c>Billing/Fulfilment/BuyerKeys</c>): two concurrent changes never leave two active keys.
/// Once committed, the change is reported to <see cref="IApiKeyCache"/>, so <c>/v1</c> applies
/// it to the next request, and audited; never before, which a rollback would belie.
/// </remarks>
internal sealed class OwnedKeys(
    LoDbDbContext db,
    IApiKeyCache cache,
    IAuditLog audit,
    TimeProvider clock)
{
    private const string Ellipsis = "…";

    /// <summary>
    /// Issues a free key named <paramref name="name"/> to <paramref name="userId"/>.
    /// </summary>
    /// <returns>The key and its secret; null when the account already has an active key.</returns>
    public async Task<KeyIssue?> CreateAsync(
        int userId,
        string? name,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await LockActiveAsync(userId, cancellationToken) is not null)
        {
            return null;
        }

        var secret = ApiKeySecrets.Generate();
        var key = Issue(userId, KeyNames.Normalize(name), secret);
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await ReportAsync(AuditAction.ApiKeyCreate, key);
        return new KeyIssue(key, secret);
    }

    /// <summary>
    /// Replaces the secret of the active key of <paramref name="userId"/>: a new key takes
    /// its name, plan, quota, rate, credits with their grants, Stripe identifiers and metered
    /// days, and the old one is revoked.
    /// </summary>
    /// <returns>The new key and its secret; null when the account has no active key.</returns>
    public async Task<KeyIssue?> RegenerateAsync(int userId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await LockActiveAsync(userId, cancellationToken) is not { } previous)
        {
            return null;
        }

        var secret = ApiKeySecrets.Generate();
        var key = Issue(userId, previous.Name, secret);
        CarryRights(previous, key);
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(cancellationToken);
        await MoveHistoryAsync(previous.Id, key.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        cache.Invalidate(previous);
        await ReportAsync(AuditAction.ApiKeyRegenerate, key);
        return new KeyIssue(key, secret);
    }

    /// <summary>Revokes the active key of <paramref name="userId"/>.</summary>
    /// <returns>False when the account has no active key.</returns>
    public async Task<bool> RevokeAsync(int userId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await LockActiveAsync(userId, cancellationToken) is not { } key)
        {
            return false;
        }

        Revoke(key);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await ReportAsync(AuditAction.ApiKeyRevoke, key);
        return true;
    }

    // The account's row, then its active key's: a credit /v1 spends meanwhile waits for the
    // commit, and is never carried to the new key as well.
    private async Task<ApiKey?> LockActiveAsync(int userId, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM users WHERE id = {userId} FOR UPDATE",
            cancellationToken);
        return await db.ApiKeys
            .FromSql(
                $"""
                SELECT * FROM api_keys WHERE user_id = {userId} AND is_active
                 ORDER BY id DESC LIMIT 1 FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }

    // A free key, until rights are carried onto it.
    private ApiKey Issue(int userId, string name, string secret) => new()
    {
        Name = name,
        KeyHash = ApiKeySecrets.Hash(secret),
        KeyPrefix = ApiKeySecrets.DisplayPrefix(secret),
        Plan = ApiPlans.Free,
        MonthlyQuota = ApiPlans.FreeQuota,
        RateLimitPerMin = ApiPlans.FreeRate,
        IsActive = true,
        CreatedAt = Now(),
        UserId = userId,
    };

    // The balance moves rather than copies: the revoked key keeps no credit to spend while
    // another instance still holds it, nor to count twice.
    private void CarryRights(ApiKey previous, ApiKey key)
    {
        key.Plan = previous.Plan;
        key.MonthlyQuota = previous.MonthlyQuota;
        key.RateLimitPerMin = previous.RateLimitPerMin;
        key.CreditsBalance = previous.CreditsBalance;
        key.StripeCustomerId = previous.StripeCustomerId;
        key.StripeSubscriptionId = previous.StripeSubscriptionId;
        previous.CreditsBalance = 0;
        Revoke(previous);
    }

    // The metered days follow, so that a new secret never resets the month's quota; the
    // grants too, so that the credits keep their expiry.
    private async Task MoveHistoryAsync(int from, int to, CancellationToken cancellationToken)
    {
        await db.ApiUsage
            .Where(usage => usage.ApiKeyId == from)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.ApiKeyId, to), cancellationToken);
        await db.ApiCreditGrants
            .Where(grant => grant.ApiKeyId == from)
            .ExecuteUpdateAsync(set => set.SetProperty(g => g.ApiKeyId, to), cancellationToken);
    }

    private void Revoke(ApiKey key)
    {
        key.IsActive = false;
        key.RevokedAt = Now();
    }

    private async Task ReportAsync(AuditAction action, ApiKey key)
    {
        cache.Invalidate(key);

        // Never cancelled: the change is stored, and the journal does its best anyway.
        await audit.RecordAsync(
            new AuditEvent
            {
                Action = action,
                Target = new AuditTarget(
                    AuditTargetType.ApiKey,
                    key.Id.ToString(CultureInfo.InvariantCulture),
                    key.KeyPrefix + Ellipsis),
            },
            CancellationToken.None);
    }

    // The columns keep whole seconds: the answer shows what a later read will.
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}
