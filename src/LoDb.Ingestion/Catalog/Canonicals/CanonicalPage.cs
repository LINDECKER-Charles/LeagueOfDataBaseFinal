using LoDb.Domain.Paths;
using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.Canonicals;

/// <summary>
/// The page search engines index for an entity (ADR 0005, amendment of 2026-10-14): its short
/// URL at the latest version, or its page in the last version that held it.
/// </summary>
public sealed record CanonicalPage
{
    /// <summary>
    /// The version of the page; <see langword="null"/> for the latest one, whose URLs carry
    /// none.
    /// </summary>
    public PatchVersion? Version { get; init; }

    /// <summary>The entity's path in that version.</summary>
    public required CanonicalPath Path { get; init; }
}
