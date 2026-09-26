namespace LoDb.Api.Modules.Profiles.Cards.Builds;

/// <summary>
/// The source of public builds until the builds module registers its own: none, so the card
/// shows an empty list.
/// </summary>
internal sealed class NoPublicBuilds : IPublicBuildSource
{
    public Task<IReadOnlyList<PublicBuildRow>> ListAsync(
        int ownerId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PublicBuildRow>>([]);
}
