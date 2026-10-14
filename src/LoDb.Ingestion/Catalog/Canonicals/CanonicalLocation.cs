namespace LoDb.Ingestion.Catalog.Canonicals;

/// <summary>Where a page is canonical, once decided.</summary>
/// <param name="Canonical">
/// The page search engines index for its entity; <see langword="null"/> keeps the page out of
/// the index.
/// </param>
public sealed record CanonicalLocation(CanonicalPage? Canonical);
