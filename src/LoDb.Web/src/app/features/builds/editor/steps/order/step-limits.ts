/**
 * The bounds of a purchase order, the API's own (`BuildStructureValidator`): 1 to 10 steps,
 * 1 to 8 items each, 40 items in all, duplicates allowed. The editor enforces them as the
 * author works; the API checks them again on save.
 */
export const STEP_LIMITS = {
  maxSteps: 10,
  minSteps: 1,
  maxItemsPerStep: 8,
  maxTotalItems: 40,
  maxLabelLength: 40,
  maxNoteLength: 300,
} as const;
