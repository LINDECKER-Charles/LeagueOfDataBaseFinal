using LoDb.Api.Modules.Catalog.Shared;

namespace LoDb.Api.Modules.Builds.Views;

/// <summary>An item of a build's purchase order; a ghost when the patch lacks it.</summary>
internal sealed record ItemView
{
    public required string Id { get; init; }

    /// <summary>The item's name; its id for a ghost.</summary>
    public required string Name { get; init; }

    public required CatalogImage Image { get; init; }

    /// <summary>Its total cost; null for a ghost, which counts for nothing in a total.</summary>
    public int? Gold { get; init; }

    public required bool Missing { get; init; }
}
