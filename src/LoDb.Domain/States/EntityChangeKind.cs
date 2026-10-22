namespace LoDb.Domain.States;

/// <summary>
/// How a state of an entity begins, as its opener shows it (« Nouveau dans ce patch »,
/// « Ce qui a changé dans ce patch », « De retour », nothing at the start of the coverage).
/// </summary>
/// <remarks>
/// A fact of the index, not of the diff engine: the engine only compares two entries, and a
/// return is a comparison with the last version of the state before the absence.
/// </remarks>
public enum EntityChangeKind
{
    /// <summary>The entity's first state: no version before it held the entity.</summary>
    New,

    /// <summary>The entity's displayed projection differs from the version before.</summary>
    Changed,

    /// <summary>The entity is back after versions without it.</summary>
    Returned,

    /// <summary>
    /// The entity's first state, opened at the start of the index's coverage: no version
    /// before it was compared, so it tells neither what it changed nor that it is new.
    /// </summary>
    Baseline,
}
