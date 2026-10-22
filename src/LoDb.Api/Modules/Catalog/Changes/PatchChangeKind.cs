namespace LoDb.Api.Modules.Catalog.Changes;

/// <summary>What a patch did to an entity, as its page of changes groups it.</summary>
internal enum PatchChangeKind
{
    /// <summary>The entity appears: its first state opens at the version.</summary>
    New,

    /// <summary>A state opens at the version, after one that ended just before it.</summary>
    Changed,

    /// <summary>The entity is back after versions without it.</summary>
    Returned,

    /// <summary>The version no longer holds the entity.</summary>
    Retired,
}
