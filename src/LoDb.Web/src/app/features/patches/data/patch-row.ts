import type { PatchListLink } from './patch-list-link';

/** A version of the history: its number, whether it is the current patch, its lists. */
export interface PatchRow {
  readonly version: string;
  readonly isLatest: boolean;
  readonly lists: readonly PatchListLink[];
}
