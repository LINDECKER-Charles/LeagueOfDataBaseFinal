using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Ddragon;

/// <summary>
/// Data Dragon's global lists: the versions and the languages.
/// </summary>
/// <remarks>
/// Both are read live, never cached here: a failure throws an
/// <see cref="Egress.Errors.EgressException"/> instead of passing for an empty list.
/// </remarks>
public interface IDdragonClient
{
    /// <summary>
    /// The versions of <c>versions.json</c>, newest first, without the legacy
    /// <c>lolpatch_*</c> entries (UP 4).
    /// </summary>
    /// <exception cref="UpstreamDocumentException">The list is absent or unreadable.</exception>
    Task<IReadOnlyList<PatchVersion>> GetVersionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The languages of <c>languages.json</c>, in upstream order. The list is global: a
    /// version may lack any of them but <c>en_US</c> (UP 2).
    /// </summary>
    /// <exception cref="UpstreamDocumentException">The list is absent or unreadable.</exception>
    Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(CancellationToken cancellationToken);
}
