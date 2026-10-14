using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Catalog.Canonicals;

/// <summary>
/// Where the entity pages of one version are canonical (ADR 0005, amendment of 2026-10-14),
/// decided from the en_US catalogs of the latest version and of the version listed after it.
/// </summary>
/// <remarks>
/// <para>
/// The id decides, as in the URLs: an entity the latest version holds is indexed there, under
/// its short URL and the path the latest version gives it, whatever its page was called
/// before. An entity the latest version dropped is indexed in the last version that held it,
/// the one the next listed version no longer holds; its pages in the versions before carry
/// no canonical, and stay out of the index.
/// </para>
/// <para>
/// Presence and paths do not depend on the language: the en_US catalogs answer for every
/// locale. Before any promotion, and in a version listed after the latest one (ingested ahead
/// of its promotion), an entity the latest version lacks is indexed at its own page.
/// </para>
/// </remarks>
public sealed record CanonicalReferences
{
    /// <summary>The version whose pages are located.</summary>
    public required PatchVersion Version { get; init; }

    /// <summary>The latest version; <see langword="null"/> before any promotion.</summary>
    public PatchVersion? Latest { get; init; }

    /// <summary>
    /// The en_US catalog of <see cref="Latest"/>; <see langword="null"/> when
    /// <see cref="Version"/> is the latest one, or before any promotion.
    /// </summary>
    public CatalogSnapshot? LatestCatalog { get; init; }

    /// <summary>
    /// The en_US catalog of the version listed after <see cref="Version"/>, the latest one's
    /// when it is that one; <see langword="null"/> when none is listed after it.
    /// </summary>
    public CatalogSnapshot? NextCatalog { get; init; }

    /// <summary>
    /// Where the page of the entity <paramref name="path"/> names, in <see cref="Version"/>,
    /// is canonical; <see langword="null"/> when that page stays out of the index.
    /// </summary>
    public CanonicalPage? Locate(CanonicalPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (Version == Latest)
        {
            return new CanonicalPage { Path = path };
        }

        if (LatestCatalog?.PageOf(path) is { } current)
        {
            return new CanonicalPage { Path = current };
        }

        var isLastHolder = Version > Latest || NextCatalog?.PageOf(path) is null;
        return isLastHolder ? new CanonicalPage { Version = Version, Path = path } : null;
    }

    /// <summary>
    /// Whether the page of the entity <paramref name="path"/> names is its own canonical: the
    /// pages the sitemap of <see cref="Version"/> lists.
    /// </summary>
    public bool IsOwnCanonical(CanonicalPath path) =>
        Locate(path) is { Version: { } version } && version == Version;
}
