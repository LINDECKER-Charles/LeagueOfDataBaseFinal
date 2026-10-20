namespace LoDb.Infrastructure.PageStore;

/// <summary>How an object is kept by the store, chosen on each write.</summary>
/// <remarks>
/// Scaleway has no bucket-wide default class: an object written without one is Standard
/// Multi-AZ. The name of the One Zone class is a setting
/// (<see cref="PageStoreOptions.OneZoneStorageClass"/>), as the development store does not know
/// Scaleway's.
/// </remarks>
public enum PageStorageClass
{
    /// <summary>Kept in several zones: the build files, which no pass can write again.</summary>
    Standard,

    /// <summary>Kept in one zone, cheaper: the pages, which a pass can render again.</summary>
    OneZone,
}
