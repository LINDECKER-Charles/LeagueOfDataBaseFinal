using LoDb.Api.Modules.Catalog.Shared;

namespace LoDb.Api.Modules.Profiles.Favorites.Views;

/// <summary>A favorite as the resolved patch shows it.</summary>
internal sealed record ResolvedFavorite
{
    public required string Id { get; init; }

    /// <summary>The name in the language asked for.</summary>
    public required string Name { get; init; }

    public required CatalogImage Image { get; init; }
}
