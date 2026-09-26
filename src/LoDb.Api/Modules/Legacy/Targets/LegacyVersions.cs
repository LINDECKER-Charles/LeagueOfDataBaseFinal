using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Api.Modules.Legacy.Targets;

/// <summary>
/// Picks the version of an old catalog URL: its path segment, else its <c>?version=</c>,
/// else the latest.
/// </summary>
/// <remarks>
/// The old site ignored an invalid <c>?version=</c> and rendered its default instead: so
/// does the redirect. A path segment was the page's identity: one Data Dragon does not list
/// makes the old URL unknown.
/// </remarks>
internal static class LegacyVersions
{
    /// <param name="pathVersion">The <c>/{version}/</c> segment, if the path had one.</param>
    /// <param name="queryVersion">The old <c>?version=</c>, as received.</param>
    /// <param name="versions">What Data Dragon lists and the latest promoted version.</param>
    /// <returns>The version; <see langword="null"/> when the path names an unknown one.</returns>
    public static LegacyVersion? Choose(
        string? pathVersion,
        string? queryVersion,
        CatalogVersions versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var fromPath = Listed(pathVersion, versions);
        if (pathVersion is not null && fromPath is null)
        {
            return null;
        }

        var requested = fromPath ?? Listed(queryVersion, versions);
        return new LegacyVersion
        {
            Pinned = requested == versions.Latest ? null : requested,
            Catalog = requested ?? versions.Latest,
        };
    }

    private static PatchVersion? Listed(string? text, CatalogVersions versions) =>
        PatchVersion.TryParse(text, out var version) && versions.Listed.Contains(version)
            ? version
            : null;
}
