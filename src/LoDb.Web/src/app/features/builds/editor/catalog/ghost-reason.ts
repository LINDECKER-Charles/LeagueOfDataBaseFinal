/**
 * Why a placed item is drawn as a ghost: `patch`, no list of this visit knows it; `mode`,
 * another game mode's list does, this one excludes it. Null for an item shown as it is.
 */
export type GhostReason = 'patch' | 'mode' | null;
