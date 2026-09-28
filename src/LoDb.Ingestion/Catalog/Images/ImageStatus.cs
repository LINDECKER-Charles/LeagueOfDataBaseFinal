namespace LoDb.Ingestion.Catalog.Images;

/// <summary>What an image resolves to.</summary>
public enum ImageStatus
{
    /// <summary>Stored: served from <c>/cdn/blobs/</c>.</summary>
    Present,

    /// <summary>Absent upstream for good (403/404): a placeholder, never fetched again.</summary>
    Absent,

    /// <summary>
    /// Not settled yet: queued, refused by the queue, failed transiently or not asked for. A
    /// placeholder for now; a later read may resolve it.
    /// </summary>
    Pending,
}
