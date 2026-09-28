namespace LoDb.Domain.Builds.Editing;

/// <summary>A secondary perk and the minor row it was picked in.</summary>
/// <param name="SlotIndex">
/// The row, from 1; <see cref="RuneDraftRules.GhostSlot"/> for a perk the patch lacks.
/// </param>
/// <param name="PerkId">The Data Dragon perk id.</param>
public sealed record SecondaryPick(int SlotIndex, int PerkId);
