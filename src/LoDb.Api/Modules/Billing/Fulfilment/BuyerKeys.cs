using System.Globalization;
using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Keys;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Billing.Fulfilment;

/// <summary>
/// The key a paid entitlement lands on: the buyer's active one, or a free one issued for it,
/// as the legacy <c>ApiEntitlementApplier</c> does for a buyer who revoked theirs meanwhile.
/// </summary>
/// <remarks>
/// The secret of an issued key is thrown away: its owner regenerates it from the portal, which
/// keeps the entitlements on a key whose secret they see.
/// </remarks>
internal sealed partial class BuyerKeys(
    LoDbDbContext db,
    BillingEffects effects,
    TimeProvider clock,
    ILogger<BuyerKeys> logger)
{
    private const string SourceKey = "source";
    private const string StripeSource = "stripe";

    /// <summary>The active key of <paramref name="userId"/>; null for an unknown account.</summary>
    public async Task<ApiKey?> ActiveKeyAsync(int userId, CancellationToken cancellationToken)
    {
        // The account's row is locked until the transaction ends: two events of a buyer
        // without a key issue one key, not two.
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM users WHERE id = {userId} FOR UPDATE",
            cancellationToken);
        var buyer = await db.Users
            .Where(user => user.Id == userId)
            .Select(user => new { user.UserName })
            .FirstOrDefaultAsync(cancellationToken);
        if (buyer is null)
        {
            LogUnknownBuyer(logger, userId);
            return null;
        }

        return await db.ApiKeys.ActiveOfAsync(userId, cancellationToken)
            ?? await IssueAsync(userId, buyer.UserName, cancellationToken);
    }

    private async Task<ApiKey> IssueAsync(
        int userId,
        string? username,
        CancellationToken cancellationToken)
    {
        var secret = ApiKeySecrets.Generate();
        var key = new ApiKey
        {
            Name = ApiKeySecrets.DefaultName,
            KeyHash = ApiKeySecrets.Hash(secret),
            KeyPrefix = ApiKeySecrets.DisplayPrefix(secret),
            Plan = ApiPlans.Free,
            MonthlyQuota = ApiPlans.FreeQuota,
            RateLimitPerMin = ApiPlans.FreeRate,
            IsActive = true,
            CreatedAt = clock.GetUtcNow(),
            UserId = userId,
        };
        db.ApiKeys.Add(key);
        // Saved at once: the entitlement updates the row by its id.
        await db.SaveChangesAsync(cancellationToken);
        effects.Audit(Issued(key, AuditActor.User(userId, username)));
        LogIssued(logger, key.Id);
        return key;
    }

    private static AuditEvent Issued(ApiKey key, AuditActor buyer) => new()
    {
        Action = AuditAction.ApiKeyCreate,
        Actor = buyer,
        Target = new AuditTarget(
            AuditTargetType.ApiKey,
            key.Id.ToString(CultureInfo.InvariantCulture),
            key.KeyPrefix + "…"),
        Meta = new Dictionary<string, object?> { [SourceKey] = StripeSource },
    };

    [LoggerMessage(
        EventName = "billing.key.issued",
        Level = LogLevel.Information,
        Message = "API key {ApiKeyId} issued for a payment whose buyer had no active key.")]
    private static partial void LogIssued(ILogger logger, int apiKeyId);

    [LoggerMessage(
        EventName = "billing.buyer.unknown",
        Level = LogLevel.Warning,
        Message = "A payment names the account {UserId}, which does not exist: not applied.")]
    private static partial void LogUnknownBuyer(ILogger logger, int userId);
}
