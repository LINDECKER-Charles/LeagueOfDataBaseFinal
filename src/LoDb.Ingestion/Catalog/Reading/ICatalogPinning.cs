namespace LoDb.Ingestion.Catalog.Reading;

/// <summary>
/// Keeps the catalogs of the latest version in memory, out of reach of eviction, so that a
/// client sweeping old versions and languages never makes the pages most read cold.
/// </summary>
/// <remarks>
/// The languages are <c>LoDb:Catalog:PinnedLanguages</c>, in their order, as many as fit in
/// <c>LoDb:Catalog:PinnedBudgetMegabytes</c> by an estimate from their stored datasets. Memory
/// of the instance, so every instance pins its own: no lock across instances.
/// </remarks>
public interface ICatalogPinning
{
    /// <summary>
    /// Pins the catalogs of the latest version, loading those not held yet, and lets those of
    /// the version it replaces become evictable. Nothing before the first promotion.
    /// </summary>
    /// <remarks>
    /// Idempotent and cheap once done: call it again until the latest version changes. A
    /// load refused or pending is left for the next call.
    /// </remarks>
    Task PinLatestAsync(CancellationToken cancellationToken);
}
