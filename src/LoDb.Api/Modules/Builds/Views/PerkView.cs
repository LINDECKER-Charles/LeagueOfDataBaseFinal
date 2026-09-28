using LoDb.Api.Modules.Catalog.Shared;

namespace LoDb.Api.Modules.Builds.Views;

/// <summary>
/// A rune a build picked, looked up in the path it was picked in; a ghost when that path
/// lacks it on the patch, or when the slot holds no pick.
/// </summary>
internal sealed record PerkView
{
    /// <summary>The rune's id; 0 for a slot without a pick.</summary>
    public required int Id { get; init; }

    /// <summary>Null for a ghost.</summary>
    public string? Key { get; init; }

    /// <summary>The rune's name; its id for a ghost.</summary>
    public required string Name { get; init; }

    public required CatalogImage Icon { get; init; }

    /// <summary>Null for a ghost.</summary>
    public string? ShortDesc { get; init; }

    public required bool Missing { get; init; }
}
