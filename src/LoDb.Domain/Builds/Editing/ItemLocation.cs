namespace LoDb.Domain.Builds.Editing;

/// <summary>A position in the purchase order: a step and an index within its items.</summary>
/// <param name="Step">The step's index, from 0.</param>
/// <param name="Index">The item's index in the step, or the insertion point of a drop.</param>
public sealed record ItemLocation(int Step, int Index);
