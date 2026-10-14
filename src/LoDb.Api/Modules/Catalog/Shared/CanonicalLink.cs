using LoDb.Ingestion.Catalog.Canonicals;

namespace LoDb.Api.Modules.Catalog.Shared;

/// <summary>
/// The page search engines index for the entity of a detail (ADR 0005, amendment of
/// 2026-10-14), which the front writes as the canonical of a past version's page.
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

    public static CanonicalLink? Of(CanonicalPage? page) => page is null
        ? null
        : new CanonicalLink { Version = page.Version?.Value, Path = page.Path.Value };
}
