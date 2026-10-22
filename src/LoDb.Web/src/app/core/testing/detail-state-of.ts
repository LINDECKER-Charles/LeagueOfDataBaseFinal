import type { DetailStateFields } from './detail-state-fields';

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
    isPresentInLatest: true,
    changes: null,
  };
}
