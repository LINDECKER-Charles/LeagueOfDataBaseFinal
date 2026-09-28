using LoDb.Api.Modules.Admin.Http;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Builds;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Builds;

/// <summary>
/// The build moderation list: the builds whose name or champion holds the search, newest
/// first, with their net score.
/// </summary>
internal sealed class BuildDirectory(LoDbDbContext db)
{
    public async Task<AdminBuildPage> SearchAsync(
        BuildSearch search,
        int page,
        CancellationToken cancellationToken)
    {
        var matching = Matching(search);
        var total = await matching.CountAsync(cancellationToken);
        var rows = await Rows(matching
                .OrderByDescending(static build => build.CreatedAt)
                .ThenByDescending(static build => build.Id)
                .Skip(AdminPaging.Skip(page))
                .Take(AdminPaging.PageSize))
            .ToListAsync(cancellationToken);
        var builds = db.Builds.AsNoTracking();
        var stats = new AdminBuildStats
        {
            Total = await builds.CountAsync(cancellationToken),
            Public = await builds.CountAsync(static build => build.IsPublic, cancellationToken),
        };
        return new AdminBuildPage
        {
            Stats = stats,
            Items = rows,
            Total = total,
            Page = page,
            Pages = AdminPaging.Pages(total),
        };
    }

    private IQueryable<Build> Matching(BuildSearch search)
    {
        var builds = db.Builds.AsNoTracking();
        if (SearchPattern.Normalize(search.Text) is { } text)
        {
            var pattern = SearchPattern.Containing(text);
            builds = builds.Where(build =>
                EF.Functions.ILike(build.Name, pattern, SearchPattern.Escape)
                || EF.Functions.ILike(build.ChampionId, pattern, SearchPattern.Escape));
        }

        return BuildVisibility.Parse(search.Visibility) is { } isPublic
            ? builds.Where(build => build.IsPublic == isPublic)
            : builds;
    }

    private IQueryable<AdminBuildRow> Rows(IQueryable<Build> builds) =>
        builds.Select(build => new AdminBuildRow
        {
            Id = build.Id,
            Name = build.Name,
            ChampionId = build.ChampionId,
            GameVersion = build.GameVersion,
            GameMode = build.GameMode,
            Language = build.Language,
            IsPublic = build.IsPublic,
            Score = db.BuildVotes
                .Where(vote => vote.BuildId == build.Id)
                .Sum(static vote => (int?)vote.Value) ?? 0,
            Owner = new AdminUserRef
            {
                Id = build.OwnerId,
                Username = build.Owner!.UserName ?? string.Empty,
                IsBanned = build.Owner.IsBanned,
            },
            ShareToken = build.ShareToken,
            CreatedAt = build.CreatedAt,
        });
}
