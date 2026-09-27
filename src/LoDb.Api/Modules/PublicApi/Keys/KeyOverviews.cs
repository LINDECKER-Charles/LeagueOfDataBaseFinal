using LoDb.Api.Modules.Billing.Keys;
using LoDb.Api.Modules.PublicApi.Keys.Contracts;
using LoDb.Api.Modules.PublicApi.Metering;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.PublicApi.Keys;

/// <summary>
/// What the portal shows of a key: its rights as stored, and its consumption as metered in
/// <c>api_usage</c>, whose last second may not be written yet.
/// </summary>
internal sealed class KeyOverviews(LoDbDbContext db, UsageCalendar calendar)
{
    // The legacy portal lists the days from 30 days ago, today included: a window that
    // always holds the whole current month.
    private const int UsageWindowDays = 30;

    /// <summary>The overview of the active key of <paramref name="userId"/>, if any.</summary>
    public async Task<ApiKeyOverview?> OfUserAsync(int userId, CancellationToken cancellationToken)
    {
        var key = await db.ApiKeys.AsNoTracking().ActiveOfAsync(userId, cancellationToken);
        return key is null ? null : await OfAsync(key, cancellationToken);
    }

    /// <summary>The overview of <paramref name="key"/>, read afresh.</summary>
    public async Task<ApiKeyOverview> OfAsync(ApiKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var today = calendar.Today;
        var month = UsageCalendar.MonthOf(today);
        var from = today.AddDays(-UsageWindowDays);
        var days = await db.ApiUsage
            .AsNoTracking()
            .Where(usage => usage.ApiKeyId == key.Id && usage.Day >= from)
            .OrderByDescending(usage => usage.Day)
            .Select(usage => new ApiUsageDay { Day = usage.Day, Requests = usage.Requests })
            .ToListAsync(cancellationToken);
        var used = days.Where(day => day.Day >= month).Sum(static day => day.Requests);
        return new ApiKeyOverview
        {
            Prefix = key.KeyPrefix,
            Name = key.Name,
            CreatedAt = key.CreatedAt,
            Plan = key.Plan,
            MonthlyQuota = key.MonthlyQuota,
            UsedThisMonth = used,
            RemainingThisMonth = Math.Max(0, key.MonthlyQuota - used),
            CreditsBalance = key.CreditsBalance,
            RateLimitPerMin = key.RateLimitPerMin,
            Subscribed = key.StripeSubscriptionId is not null,
            Usage = days,
        };
    }
}
