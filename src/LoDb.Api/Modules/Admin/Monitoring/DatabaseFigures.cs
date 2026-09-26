using LoDb.Api.Modules.Admin.Monitoring.Views;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Monitoring;

/// <summary>
/// The figures of the monitoring page read from the database: the application counters,
/// the e-mail outbox and the weight of the main tables.
/// </summary>
internal sealed class DatabaseFigures(LoDbDbContext db, TimeProvider clock)
{
    private static readonly TimeSpan NewAccountWindow = TimeSpan.FromDays(7);

    public async Task<AppCounters> CountersAsync(CancellationToken cancellationToken)
    {
        var since = clock.GetUtcNow() - NewAccountWindow;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var month = new DateOnly(today.Year, today.Month, 1);
        var users = db.Users.AsNoTracking();
        var usage = db.ApiUsage.AsNoTracking();
        return new AppCounters
        {
            UsersTotal = await users.CountAsync(cancellationToken),
            UsersNewWeek = await users.CountAsync(u => u.CreatedAt >= since, cancellationToken),
            UsersBanned = await users.CountAsync(static u => u.IsBanned, cancellationToken),
            BuildsTotal = await db.Builds.CountAsync(cancellationToken),
            BuildsPublic = await db.Builds.CountAsync(static b => b.IsPublic, cancellationToken),
            Votes = await db.BuildVotes.CountAsync(cancellationToken),
            DonationsCount = await db.Donations.CountAsync(cancellationToken),
            DonationsTotalCents = await db.Donations
                .SumAsync(static d => (long)d.AmountCents, cancellationToken),
            ApiKeysActive = await db.ApiKeys
                .CountAsync(static k => k.IsActive && k.RevokedAt == null, cancellationToken),
            ApiRequestsToday = await usage.Where(u => u.Day == today)
                .SumAsync(static u => (long?)u.Requests, cancellationToken) ?? 0,
            ApiRequestsMonth = await usage.Where(u => u.Day >= month)
                .SumAsync(static u => (long?)u.Requests, cancellationToken) ?? 0,
        };
    }

    /// <summary>E-mails pending, then e-mails given up.</summary>
    public async Task<(int Pending, int Dead)?> OutboxAsync(CancellationToken cancellationToken)
    {
        var outbox = db.EmailOutbox.AsNoTracking();
        var pending = await outbox.CountAsync(
            static mail => mail.Status == EmailOutboxStatus.Pending,
            cancellationToken);
        var dead = await outbox.CountAsync(
            static mail => mail.Status == EmailOutboxStatus.Dead,
            cancellationToken);
        return (pending, dead);
    }

    /// <summary>
    /// The tables that grow with the audience, partitions summed: the analytics events are
    /// partitioned by day.
    /// </summary>
    public async Task<IReadOnlyList<TableVolume>> TablesAsync(CancellationToken cancellation) =>
        await db.Database.SqlQuery<TableVolume>(
                $"""
                SELECT c.relname AS "Name",
                       (SELECT COALESCE(sum(pg_total_relation_size(p.relid)), 0)
                          FROM pg_partition_tree(c.oid) p)::bigint AS "Bytes"
                  FROM pg_class c
                  JOIN pg_namespace n ON n.oid = c.relnamespace
                 WHERE n.nspname = current_schema()
                   AND c.relkind IN ('r', 'p')
                   AND c.relname IN ('users', 'builds', 'build_votes', 'donations',
                                     'api_keys', 'api_usage', 'contact_messages',
                                     'audit_log', 'analytics_events', 'analytics_daily',
                                     'email_outbox')
                 ORDER BY 2 DESC, 1
                """)
            .ToListAsync(cancellation);
}
