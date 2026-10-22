using LoDb.Domain.Catalog;

namespace LoDb.Api.Modules.Catalog.Changes;

/// <summary>The entities of one resource a patch changed.</summary>
internal sealed record PatchChangeGroup
{
    public required ResourceType Type { get; init; }

    /// <summary>New, changed, returned, then retired entities, each by name.</summary>
    public required IReadOnlyList<PatchChangeEntry> Entries { get; init; }
}
