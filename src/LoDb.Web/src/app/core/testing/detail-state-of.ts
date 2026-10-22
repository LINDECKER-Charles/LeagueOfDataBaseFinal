import type { ItemDetails } from '../api/generated/models/item-details';

/** The fields of the index's rule a detail of every resource carries. */
export type DetailStateFields = Pick<
  ItemDetails,
  'canonical' | 'state' | 'history' | 'next' | 'presentInLatest' | 'changes'
>;

/**
 * What a spec of a detail leaves unsaid of its state: a page of the latest version, its own
 * short URL, outside the index (no state, no history, no change).
 */
export function detailStateOf(path: string): DetailStateFields {
  return {
    canonical: { version: null, path },
    state: null,
    history: [],
    next: null,
    presentInLatest: true,
    changes: null,
  };
}
