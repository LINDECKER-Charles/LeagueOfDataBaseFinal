using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Profiles.Cards.Builds;
using LoDb.Api.Modules.Profiles.Showcase;
using LoDb.Domain.Catalog.Champions;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Ingestion.Catalog;

namespace LoDb.Api.Modules.Profiles.Cards;

/// <summary>
/// Builds the public card of a profile, shared by the public route and the owner's preview.
/// </summary>
internal sealed class PublicCards(IPublicBuildSource builds)
{
    /// <param name="user">The profile's account.</param>
    /// <param name="context">The catalog of its patch; null when none could be read.</param>
    /// <param name="cancellationToken">Aborts the reads.</param>
    public async Task<PublicProfile> BuildAsync(
        User user,
        CatalogContext? context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var showcase = await ProfileShowcase.ResolveAsync(user, context, cancellationToken);
        return new PublicProfile
        {
            Username = user.UserName ?? string.Empty,
            RiotTagline = user.RiotTagline,
            IsSupporter = user.IsSupporter,
            IsPublic = user.IsPublicProfile,
            MemberSince = user.CreatedAt,
            Showcase = showcase,
            Backdrop = ProfileBackdrop.Of(showcase.Skin, user.FavoriteChampionId),
            Builds = await CardsAsync(user.Id, context, cancellationToken),
        };
    }

    // A portrait is decorative: a champion the patch lacks leaves its card without one.
    private async Task<IReadOnlyList<ProfileBuildCard>> CardsAsync(
        int ownerId,
        CatalogContext? context,
        CancellationToken cancellationToken)
    {
        var rows = await builds.ListAsync(ownerId, cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        List<ChampionDetail?> champions =
            [.. rows.Select(row => context?.Catalog.Champions.Find(row.ChampionId))];
        var images = context is null
            ? ImageSet.Empty
            : await context.ResolveAsync(
                champions.OfType<ChampionDetail>().Select(EntityImages.Portrait),
                ColdDemand.Synchronous,
                cancellationToken);
        return [.. rows.Select((row, index) =>
            ProfileBuildCard.Of(row, champions[index], images))];
    }
}
