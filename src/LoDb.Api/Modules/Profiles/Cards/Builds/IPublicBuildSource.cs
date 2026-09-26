namespace LoDb.Api.Modules.Profiles.Cards.Builds;

/// <summary>
/// The builds an owner published, which the public card lists: the extension point of the
/// builds module.
/// </summary>
/// <remarks>
/// The profiles module registers <see cref="NoPublicBuilds"/> with <c>TryAdd</c>, so the card
/// lists no build until the builds module (L5.1) registers its own source with <c>AddScoped</c>
/// or <c>Replace</c>; nothing in this module changes then.
/// </remarks>
internal interface IPublicBuildSource
{
    /// <summary>
    /// The public builds of <paramref name="ownerId"/>, most recently updated first; the
    /// card resolves their champions on the profile's own patch.
    /// </summary>
    Task<IReadOnlyList<PublicBuildRow>> ListAsync(int ownerId, CancellationToken cancellationToken);
}
