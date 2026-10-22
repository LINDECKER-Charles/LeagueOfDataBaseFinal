using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Catalog.States.Canonicals;

namespace LoDb.Api.Modules.Catalog.Shared;

/// <summary>
/// A page of an entity as the front links it, or writes it as the canonical of a page: its
/// short URL, or its page in a version (ADR 0005, amendment of 2026-10-22).
/// </summary>
internal sealed record CanonicalLink
{
    /// <summary>
    /// The version of that page; <see langword="null"/> for the latest one, under its short
    /// URL.
    /// </summary>
    public required string? Version { get; init; }

    /// <summary>The entity's path in that version.</summary>
    public required string Path { get; init; }

    /// <summary>
    /// The page itself, in its version: a detail's canonical until the rule decides another.
    /// </summary>
    public static CanonicalLink PageItself(CatalogSnapshot catalog, string path)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new CanonicalLink { Version = catalog.Version.Value, Path = path };
    }

    public static CanonicalLink Of(CanonicalTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new CanonicalLink { Version = target.Version?.Value, Path = target.Path };
    }
}
