namespace LoDb.Api.Modules.Admin.Prerender.Units.Waits;

/// <summary>Why the units of a version wait in a pass.</summary>
internal enum UnitWaitReason
{
    /// <summary>Its catalog is not stored yet: an ingestion is queued for it.</summary>
    CatalogPending,

    /// <summary>The API does not count it among the older patches yet.</summary>
    NotPinned,

    /// <summary>
    /// It just left the latest rank, and some server still holds it for the latest one.
    /// </summary>
    Settling,
}
