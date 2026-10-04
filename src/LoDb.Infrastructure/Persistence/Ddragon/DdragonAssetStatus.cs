namespace LoDb.Infrastructure.Persistence.Ddragon;

/// <summary>Outcome of an asset's ingestion, stored as <c>present</c> or <c>absent</c>.</summary>
public enum DdragonAssetStatus
{
    /// <summary>The blob exists; the row gives its SHA-256 and extension.</summary>
    Present,

    /// <summary>
    /// Definitive absence (403/404): rendered as a placeholder, never fetched again.
    /// </summary>
    Absent,
}
