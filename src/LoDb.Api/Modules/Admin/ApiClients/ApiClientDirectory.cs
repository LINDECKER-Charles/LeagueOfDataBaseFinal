using LoDb.Api.Modules.Admin.ApiClients.Views;
using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.ApiClients;

/// <summary>
/// The keys of the public API, newest first, with what they consumed this UTC month, the
/// counters of the fleet and its heaviest users.
/// </summary>
internal sealed class ApiClientDirectory(LoDbDbContext db, TimeProvider clock)
{
    private const int TopConsumerDays = 30;
    private const int TopConsumerCount = 8;

    public async Task<AdminApiClientPage> ListAsync(int page, CancellationToken cancellationToken)
    {
        var month = MonthStart();
        var keys = db.ApiKeys.AsNoTracking();
        var total = await keys.CountAsync(cancellationToken);
        var rows = await Rows(keys
                .OrderByDescending(static key => key.CreatedAt)
                .ThenByDescending(static key => key.Id)
                .Skip(AdminPaging.Skip(page))
                .Take(AdminPaging.PageSize), month)
            .ToListAsync(cancellationToken);
        return new AdminApiClientPage
        {
            Kpis = await KpisAsync(month, cancellationToken),
            TopConsumers = await TopConsumersAsync(cancellationToken),
            Items = rows,
            Total = total,
            Page = page,
            Pages = AdminPaging.Pages(total),
        };
    }

    private IQueryable<AdminApiClientRow> Rows(IQueryable<ApiKey> keys, DateOnly month) =>
        keys.Select(key => new AdminApiClientRow
        {
            Id = key.Id,
            Name = key.Name,
            KeyPrefix = key.KeyPrefix,
            Plan = key.Plan,
            MonthlyQuota = key.MonthlyQuota,
            UsedThisMonth = db.ApiUsage
                .Where(usage => usage.ApiKeyId == key.Id && usage.Day >= month)
                .Sum(static usage => (long?)usage.Requests) ?? 0,
            CreditsBalance = key.CreditsBalance,
            RateLimitPerMin = key.RateLimitPerMin,
            IsActive = key.IsActive && key.RevokedAt == null,
            RevokedAt = key.RevokedAt,
            CreatedAt = key.CreatedAt,
            Owner = new AdminUserRef
            {
                Id = key.UserId,
                Username = key.User!.UserName!,
                IsBanned = key.User.IsBanned,
            },
        });

    private async Task<ApiClientKpis> KpisAsync(DateOnly month, CancellationToken cancellation)
    {
        var active = db.ApiKeys.AsNoTracking()
            .Where(static key => key.IsActive && key.RevokedAt == null);
        var byPlan = await active
            .GroupBy(static key => key.Plan)
            .Select(static plan => new PlanCount { Plan = plan.Key, Keys = plan.Count() })
            .OrderByDescending(static plan => plan.Keys)
            .ThenBy(static plan => plan.Plan)
            .ToListAsync(cancellation);
        return new ApiClientKpis
        {
            Active = await active.CountAsync(cancellation),
            MonthRequests = await db.ApiUsage.AsNoTracking()
                .Where(usage => usage.Day >= month)
                .SumAsync(static usage => (long?)usage.Requests, cancellation) ?? 0,
            Credits = await active.SumAsync(static key => key.CreditsBalance, cancellation),
            ByPlan = byPlan,
        };
    }

    private Task<List<TopConsumer>> TopConsumersAsync(CancellationToken cancellationToken)
    {
        var since = Today().AddDays(-TopConsumerDays);
        return db.ApiUsage.AsNoTracking()
            .Where(usage => usage.Day >= since)
            .GroupBy(static usage => usage.ApiKeyId)
            .Select(static usage => new { Id = usage.Key, Requests = usage.Sum(u => u.Requests) })
            .OrderByDescending(static usage => usage.Requests)
            .Take(TopConsumerCount)
            .Join(
                db.ApiKeys,
                static usage => usage.Id,
                static key => key.Id,
                static (usage, key) => new TopConsumer
                {
                    Id = key.Id,
                    KeyPrefix = key.KeyPrefix,
                    Username = key.User!.UserName!,
                    Requests = usage.Requests,
                })
            .OrderByDescending(static consumer => consumer.Requests)
            .ToListAsync(cancellationToken);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    // The quota month of the public API: the UTC calendar month.
    private DateOnly MonthStart()
    {
        var today = Today();
        return new DateOnly(today.Year, today.Month, 1);
    }
}
