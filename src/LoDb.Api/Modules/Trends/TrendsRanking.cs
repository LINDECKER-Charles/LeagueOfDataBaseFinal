using LoDb.Domain.Catalog.Modes;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Builds;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Trends;

/// <summary>
/// The public builds, best scored first, then newest: the ranking of the legacy trends.
/// A banned author's builds leave it, and its facets, at once.
/// </summary>
internal sealed class TrendsRanking(LoDbDbContext db)
{
    public const int PerPage = 24;

    /// <summary>The builds of page <paramref name="page"/>, and how many match in all.</summary>
    public async Task<(IReadOnlyList<RankedBuild> Builds, int Total)> RankAsync(
        TrendsFilter filter,
        int page,
        CancellationToken cancellationToken)
    {
        var listed = Listed(filter);
        var total = await listed.CountAsync(cancellationToken);
        var builds = await listed
            .Select(build => new RankedBuild
            {
                Build = build,
                Owner = build.Owner!,
                Score = db.BuildVotes
                    .Where(vote => vote.BuildId == build.Id)
                    .Sum(vote => (int?)vote.Value) ?? 0,
            })
            .OrderByDescending(static row => row.Score)
            .ThenByDescending(static row => row.Build.CreatedAt)
            .ThenByDescending(static row => row.Build.Id)
            .Skip((page - 1) * PerPage)
            .Take(PerPage)
            .ToListAsync(cancellationToken);
        return (builds, total);
    }

    /// <summary>The champions of the listed builds, by id: the champion facet.</summary>
    public async Task<IReadOnlyList<string>> ChampionIdsAsync(
        CancellationToken cancellationToken) =>
        await Listed(TrendsFilter.None)
            .Select(static build => build.ChampionId)
            .Distinct()
            .OrderBy(static id => id)
            .ToListAsync(cancellationToken);

    /// <summary>The languages of the listed builds, by code: the language facet.</summary>
    public async Task<IReadOnlyList<string>> LanguagesAsync(
        CancellationToken cancellationToken) =>
        await Listed(TrendsFilter.None)
            .Select(static build => build.Language)
            .Distinct()
            .OrderBy(static language => language)
            .ToListAsync(cancellationToken);

    private IQueryable<Build> Listed(TrendsFilter filter)
    {
        var builds = db.Builds.AsNoTracking()
            .Where(static build => build.IsPublic && !build.Owner!.IsBanned);
        if (filter.Champion is { } champion)
        {
            builds = builds.Where(build => build.ChampionId == champion);
        }

        if (filter.Mode is { } mode)
        {
            var code = GameModes.Code(mode);
            builds = builds.Where(build => build.GameMode == code);
        }

        return filter.Language is { } language
            ? builds.Where(build => build.Language == language)
            : builds;
    }
}
