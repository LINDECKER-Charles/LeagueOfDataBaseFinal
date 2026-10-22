import type { UrlTree } from '@angular/router';
import type { PatchListLink } from './patch-list-link';

/**
 * A version of the history: its number, whether it is the current patch, the page of its
 * changes where it has one, its lists.
 */
export interface PatchRow {
  readonly version: string;
  readonly isLatest: boolean;
  readonly changes: UrlTree | null;
  readonly lists: readonly PatchListLink[];
}
