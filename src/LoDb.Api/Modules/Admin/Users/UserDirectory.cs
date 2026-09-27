using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Users;

/// <summary>
/// The moderation list: the accounts whose username or e-mail holds the search, newest
/// first, with their counters.
/// </summary>
internal sealed class UserDirectory(LoDbDbContext db, TimeProvider clock)
{
    private static readonly TimeSpan NewAccountWindow = TimeSpan.FromDays(7);

    public async Task<AdminUserPage> SearchAsync(
        string? search,
        int page,
        CancellationToken cancellationToken)
    {
        var matching = Matching(SearchPattern.Normalize(search));
        var total = await matching.CountAsync(cancellationToken);
        var rows = await Rows(matching
                .OrderByDescending(static user => user.CreatedAt)
                .ThenByDescending(static user => user.Id)
                .Skip(AdminPaging.Skip(page))
                .Take(AdminPaging.PageSize))
            .ToListAsync(cancellationToken);
        return new AdminUserPage
        {
            Stats = await StatsAsync(cancellationToken),
            Items = rows,
            Total = total,
            Page = page,
            Pages = AdminPaging.Pages(total),
        };
    }

    private IQueryable<User> Matching(string? search)
    {
        var users = db.Users.AsNoTracking();
        if (search is null)
        {
            return users;
        }

        var pattern = SearchPattern.Containing(search);
        return users.Where(user =>
            EF.Functions.ILike(user.UserName!, pattern, SearchPattern.Escape)
            || EF.Functions.ILike(user.Email!, pattern, SearchPattern.Escape));
    }

    private IQueryable<AdminUserRow> Rows(IQueryable<User> users)
    {
        var admins = db.Set<IdentityUserRole<int>>()
            .Where(link => db.Set<Role>()
                .Any(role => role.Id == link.RoleId && role.Name == Role.Admin));
        return users.Select(user => new AdminUserRow
        {
            Id = user.Id,
            Username = user.UserName ?? string.Empty,
            RiotTagline = user.RiotTagline,
            Email = user.Email ?? string.Empty,
            EmailVerified = user.EmailConfirmed,
            IsBanned = user.IsBanned,
            BannedAt = user.BannedAt,
            BanReason = user.BanReason,
            IsSupporter = user.IsSupporter,
            IsPublicProfile = user.IsPublicProfile,
            Google = user.GoogleId != null,
            IsAdmin = admins.Any(link => link.UserId == user.Id),
            TwoFactorEnabled = user.TwoFactorEnabled,
            BuildCount = db.Builds.Count(build => build.OwnerId == user.Id),
            CreatedAt = user.CreatedAt,
        });
    }

    private async Task<AdminUserStats> StatsAsync(CancellationToken cancellationToken)
    {
        var since = clock.GetUtcNow() - NewAccountWindow;
        var users = db.Users.AsNoTracking();
        return new AdminUserStats
        {
            Total = await users.CountAsync(cancellationToken),
            NewWeek = await users.CountAsync(user => user.CreatedAt >= since, cancellationToken),
            Banned = await users.CountAsync(static user => user.IsBanned, cancellationToken),
            Supporters = await users.CountAsync(
                static user => user.IsSupporter,
                cancellationToken),
        };
    }
}
