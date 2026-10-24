/** The views of the page, in the order of their tabs. */
export const PRERENDER_VIEWS = [
  'pass',
  'coverage',
  'queues',
  'speed',
  'storage',
  'history',
] as const;

/**
 * A view of the page (`StatusTabs`): the active pass or what stands in for it, the coverage of
 * an epoch, the automatic queues, the speed, the storage and its costs, the history.
 */
export type PrerenderView = (typeof PRERENDER_VIEWS)[number];
