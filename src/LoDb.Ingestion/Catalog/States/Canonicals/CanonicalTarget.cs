using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Ingestion.Catalog.States.Canonicals;

/// <summary>
/// The page search engines index for a page of an entity: the short URL of the latest
/// version, or a pinned page, most often the opener of a closed state.
/// </summary>
/// <param name="Version">The version of that page; null for the short URL.</param>
/// <param name="Path">The entity's path in that version (<c>items/3371-molten-edge</c>).</param>
public sealed record CanonicalTarget(PatchVersion? Version, string Path)
{
    public static CanonicalTarget ShortUrl(string path) => new(null, path);

    public static CanonicalTarget Pinned(PatchVersion version, string path) =>
        new(version, path);

    /// <summary>The opener's own page, with the path and slug of that version.</summary>
    public static CanonicalTarget OpenerOf(IndexedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new CanonicalTarget(state.Opener, state.Path);
    }
}
