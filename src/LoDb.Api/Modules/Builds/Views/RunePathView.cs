using LoDb.Api.Modules.Catalog.Shared;

namespace LoDb.Api.Modules.Builds.Views;

/// <summary>A rune path a build picked; a ghost when the patch lacks it.</summary>
internal sealed record RunePathView
{
    /// <summary>The path's id; 0 when the build picked none.</summary>
    public required int Id { get; init; }

    /// <summary>Null for a ghost.</summary>
    public string? Key { get; init; }

    /// <summary>The path's name; its id for a ghost.</summary>
    public required string Name { get; init; }

    public required CatalogImage Icon { get; init; }

    public required bool Missing { get; init; }
}
