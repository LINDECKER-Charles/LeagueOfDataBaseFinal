using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Billing;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Donations;

/// <summary>The donations, read-only: the Stripe webhook writes them, nobody edits them.</summary>
internal sealed class DonationLedger(LoDbDbContext db, TimeProvider clock)
{
    private const int SparklineDays = 30;

    public async Task<AdminDonationPage> ListAsync(int page, CancellationToken cancellationToken)
    {
        var donations = db.Donations.AsNoTracking();
        var total = await donations.CountAsync(cancellationToken);
        var rows = await Rows(donations
                .OrderByDescending(static donation => donation.CreatedAt)
                .ThenByDescending(static donation => donation.Id)
                .Skip(AdminPaging.Skip(page))
                .Take(AdminPaging.PageSize))
            .ToListAsync(cancellationToken);
        return new AdminDonationPage
        {
            Kpis = await KpisAsync(total, cancellationToken),
            Daily = await DailyAsync(cancellationToken),
            Items = rows,
            Total = total,
            Page = page,
            Pages = AdminPaging.Pages(total),
        };
    }

    private static IQueryable<AdminDonationRow> Rows(IQueryable<Donation> donations) =>
        donations.Select(static donation => new AdminDonationRow
        {
            Id = donation.Id,
            AmountCents = donation.AmountCents,
            Currency = donation.Currency,
            CreatedAt = donation.CreatedAt,
            Donor = donation.User == null
                ? null
                : new AdminUserRef
                {
                    Id = donation.User.Id,
                    Username = donation.User.UserName!,
                    IsBanned = donation.User.IsBanned,
                },
            DonorIsSupporter = donation.User != null && donation.User.IsSupporter,
        });

    private async Task<DonationKpis> KpisAsync(int count, CancellationToken cancellationToken)
    {
        var donations = db.Donations.AsNoTracking();
        return new DonationKpis
        {
            TotalCents = await donations.SumAsync(
                static donation => (long)donation.AmountCents,
                cancellationToken),
            Count = count,
            IdentifiedDonors = await donations
                .Where(static donation => donation.UserId != null)
                .Select(static donation => donation.UserId)
                .Distinct()
                .CountAsync(cancellationToken),
            Anonymous = await donations.CountAsync(
                static donation => donation.UserId == null,
                cancellationToken),
            Supporters = await db.Users.CountAsync(
                static user => user.IsSupporter,
                cancellationToken),
        };
    }

    private async Task<IReadOnlyList<DailyAmount>> DailyAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var first = today.AddDays(1 - SparklineDays);
        var since = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var gifts = await db.Donations.AsNoTracking()
            .Where(donation => donation.CreatedAt >= since)
            .Select(static donation => new { donation.CreatedAt, donation.AmountCents })
            .ToListAsync(cancellationToken);
        var byDay = gifts
            .GroupBy(static gift => DateOnly.FromDateTime(gift.CreatedAt.UtcDateTime))
            .ToDictionary(static day => day.Key, static day => day.Sum(g => (long)g.AmountCents));
        return [.. Enumerable.Range(0, SparklineDays).Select(offset => first.AddDays(offset))
            .Select(day => new DailyAmount { Date = day, Cents = byDay.GetValueOrDefault(day) })];
    }
}
