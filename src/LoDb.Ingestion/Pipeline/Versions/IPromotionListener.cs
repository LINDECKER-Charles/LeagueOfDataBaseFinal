using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Pipeline.Versions;

/// <summary>
/// Told when a version becomes the latest one, once its promotion is saved: what the rest of
/// the site does about a new patch, such as telling search engines its pages changed.
/// </summary>
/// <remarks>
/// Called by the run that promoted the version, on its instance only. A listener that throws
/// is logged and never undoes the promotion; it is not called again for that version.
/// </remarks>
public interface IPromotionListener
{
    Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken);
}
