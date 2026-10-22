using LoDb.Domain.States;

namespace LoDb.Api.Modules.Catalog.States;

/// <summary>
/// What the state of the page changed when it opened: on an opener's page, and on the
/// short URL when the open state opened at the latest version.
/// </summary>
internal sealed record EntityChanges
{
    public required EntityChangeKind Kind { get; init; }

    /// <summary>
    /// The version compared to: the last one of the entity's previous state, a return
    /// included; null for a new entity.
    /// </summary>
    public required string? ComparedVersion { get; init; }

    /// <summary>The fields changed; none for a new entity.</summary>
    public required IReadOnlyList<FieldChange> Fields { get; init; }
}
