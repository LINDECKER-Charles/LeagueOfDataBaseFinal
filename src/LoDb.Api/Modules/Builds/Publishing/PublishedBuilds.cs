using LoDb.Api.Modules.Profiles.Cards.Builds;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Builds.Publishing;

/// <summary>
/// The builds a profile card lists: its owner's public ones, most recently updated first,
/// as the legacy card listed them.
/// </summary>
/// <remarks>
/// Replaces the empty source the profiles module registers, which then shows them without
/// a change of its own.
/// </remarks>
internal sealed class PublishedBuilds(LoDbDbContext db) : IPublicBuildSource
{
    public async Task<IReadOnlyList<PublicBuildRow>> ListAsync(
        int ownerId,
        CancellationToken cancellationToken) =>
        await db.Builds.AsNoTracking()
            .Where(build => build.OwnerId == ownerId && build.IsPublic)
            .OrderByDescending(static build => build.UpdatedAt)
            .ThenByDescending(static build => build.Id)
            .Select(static build => new PublicBuildRow
            {
                ShareToken = build.ShareToken,
                Name = build.Name,
                ChampionId = build.ChampionId,
                GameVersion = build.GameVersion,
                UpdatedAt = build.UpdatedAt,
            })
            .ToListAsync(cancellationToken);
}
