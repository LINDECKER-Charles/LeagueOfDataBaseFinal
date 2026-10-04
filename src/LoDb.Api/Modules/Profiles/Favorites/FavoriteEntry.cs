using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Profiles.Favorites;

/// <summary>A favorite found in a catalog: its name and the image still to resolve.</summary>
/// <param name="Id">The id as stored.</param>
/// <param name="Name">The name in the catalog's language.</param>
/// <param name="Image">Its Data Dragon image; null when its entry names none.</param>
internal sealed record FavoriteEntry(string Id, string Name, DdragonImage? Image);
