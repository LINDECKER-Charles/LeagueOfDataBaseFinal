namespace LoDb.Api.Modules.Catalog.States;

/// <summary>How a closed state ended.</summary>
internal enum StateNextKind
{
    /// <summary>The entity changed: its next state opened at the version after.</summary>
    Changed,

    /// <summary>The version after holds no page of the entity.</summary>
    Removed,
}
