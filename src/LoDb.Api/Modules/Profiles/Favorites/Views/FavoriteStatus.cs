namespace LoDb.Api.Modules.Profiles.Favorites.Views;

/// <summary>What a favorite slot shows.</summary>
internal enum FavoriteStatus
{
    /// <summary>Nothing is stored in the slot.</summary>
    Empty,

    /// <summary>The stored favorite is on the resolved patch.</summary>
    Resolved,

    /// <summary>
    /// A favorite is stored but could not be shown: the resolved patch lacks it, or the
    /// catalog could not be read. It stays stored all the same.
    /// </summary>
    Unavailable,
}
