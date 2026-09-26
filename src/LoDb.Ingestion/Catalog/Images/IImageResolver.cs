using LoDb.Domain.Versions;
using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Catalog.Images;

/// <summary>
/// Resolves the images of a page against the manifest (ADR 0004): present to its blob,
/// absent to a placeholder without another fetch, never tried as the caller's
/// <see cref="ColdDemand"/> allows.
/// </summary>
/// <remarks>
/// <para>
/// A detail page, the picker, a build and the search resolve synchronously: they wait for
/// the missing images, merged with any identical ingestion in flight. Lists and previews
/// queue them and show placeholders meanwhile; a crawler always queues (C5). The images an
/// entity references come from <see cref="VersionImages"/>; splash art, skins and videos are
/// hotlinked instead (<see cref="Hotlinks.ChampionHotlinks"/>).
/// </para>
/// <para>
/// Nothing is cached here: an image that failed transiently stays pending, and the next
/// resolution asks for it again.
/// </para>
/// </remarks>
public interface IImageResolver
{
    /// <summary>One manifest query, whatever the number of images.</summary>
    Task<ImageResolution> ResolveAsync(
        PatchVersion version,
        IReadOnlyCollection<DdragonImage> images,
        ColdDemand demand,
        CancellationToken cancellationToken);
}
