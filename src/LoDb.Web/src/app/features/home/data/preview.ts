import type { PreviewEntry } from './preview-entry';

/** The first entries of a resource's list and the size of the whole list. */
export interface Preview {
  readonly total: number;
  readonly entries: readonly PreviewEntry[];
}
