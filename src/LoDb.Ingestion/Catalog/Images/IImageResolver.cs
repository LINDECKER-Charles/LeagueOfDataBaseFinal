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
/// The verdicts read are memoised per version (<see cref="ImageVerdicts"/>): a page whose
/// images are all settled costs no query. Only a pending image is not settled: an image that
/// failed transiently, or was never tried, is read again by the next resolution that may
/// fetch or queue it.
/// </para>
/// </remarks>
public interface IImageResolver
{
    /// <summary>
    /// At most one manifest query for the keys the memo does not settle, whatever their
    /// number, and one more after waiting for an ingestion.
    /// </summary>
    Task<ImageResolution> ResolveAsync(
        PatchVersion version,
        IReadOnlyCollection<DdragonImage> images,
        ColdDemand demand,
        CancellationToken cancellationToken);
}
