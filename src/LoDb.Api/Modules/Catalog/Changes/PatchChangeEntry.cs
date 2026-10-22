using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Api.Modules.Catalog.States;

namespace LoDb.Api.Modules.Catalog.Changes;

/// <summary>An entity a patch changed, as its page of changes lists it.</summary>
internal sealed record PatchChangeEntry
{
    public required string Id { get; init; }

    /// <summary>
    /// Its name in the page's language: from the version's catalog, or from the version
    /// before's for a retired entity.
    /// </summary>
    public required string Name { get; init; }

    public required CatalogImage Image { get; init; }

    public required PatchChangeKind Kind { get; init; }

    /// <summary>The fields a change or a return changed; none for a new or retired one.</summary>
    public required IReadOnlyList<ChangedField> ChangedFields { get; init; }

    /// <summary>
    /// Its page at the version (the short URL when the version is the latest); for a retired
    /// entity, the opener of its last state.
    /// </summary>
    public required CanonicalLink Link { get; init; }
}
