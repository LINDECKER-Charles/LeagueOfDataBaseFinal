using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>A load of a catalog no memory holds, and who asks for it.</summary>
/// <param name="Caller">The partition of the cold gate the load is admitted in.</param>
/// <param name="Build">
/// Builds the catalog from the stored datasets; returns <see langword="null"/> when one is
/// missing.
/// </param>
internal sealed record ColdLoad(
    ColdCaller Caller,
    Func<CancellationToken, Task<CatalogSnapshot?>> Build);
