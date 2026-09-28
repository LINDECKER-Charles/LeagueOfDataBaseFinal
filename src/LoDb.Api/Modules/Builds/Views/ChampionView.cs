using LoDb.Api.Modules.Catalog.Shared;

namespace LoDb.Api.Modules.Builds.Views;

/// <summary>
/// A build's champion on the patch it renders on; a ghost when the patch lacks it.
/// </summary>
internal sealed record ChampionView
{
    public required string Id { get; init; }

    /// <summary>The champion's name; its id for a ghost, which the patch does not name.</summary>
    public required string Name { get; init; }

    /// <summary>Null for a ghost.</summary>
    public string? Title { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>
    /// Whether the patch lacks the champion: the build keeps it, and the page flags it.
    /// </summary>
    public required bool Missing { get; init; }
}
