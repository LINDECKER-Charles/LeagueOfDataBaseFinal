import type { PrerenderDemandHour } from '../../../../core/api/generated/models/prerender-demand-hour';
import type { PrerenderDemandListedPage } from '../../../../core/api/generated/models/prerender-demand-listed-page';
import type { PrerenderDemandPage } from '../../../../core/api/generated/models/prerender-demand-page';
import type { PrerenderDemandPages } from '../../../../core/api/generated/models/prerender-demand-pages';
import type { PrerenderDemandReport } from '../../../../core/api/generated/models/prerender-demand-report';

const HOUR_MS = 3_600_000;
// The oldest of the 24 hours the report charts, the current one (20:00) last.
const FIRST_HOUR = Date.parse('2026-10-20T21:00:00Z');

// The two most requested pages, the second tried once.
const TOP: readonly PrerenderDemandPage[] = [
  {
    path: '/fr/15.13.1/champions/Aatrox',
    version: '15.13.1',
    locale: 'fr',
    demand: 48,
    attempts: 0,
    lastSeenAt: '2026-10-21T19:58:00Z',
    reason: null,
  },
  {
    path: '/en/14.1.1/items/1036-long-sword',
    version: '14.1.1',
    locale: 'en',
    demand: 12,
    attempts: 1,
    lastSeenAt: '2026-10-21T19:40:00Z',
    reason: 'The SSR answered 502, not a page to store.',
  },
];

/**
 * The last 24 hours of the queue, the oldest first: 60 pages entered and 50 written each hour,
 * but the current one, 30 entered and 64 written.
 */
export function demandHours(): PrerenderDemandHour[] {
  return Array.from({ length: 24 }, (_, index) => ({
    start: new Date(FIRST_HOUR + index * HOUR_MS).toISOString(),
    entered: index === 23 ? 30 : 60,
    written: index === 23 ? 64 : 50,
  }));
}

/**
 * The automatic queue as the API reports it: active, reading the SSR, 412 pages pending, the
 * two most requested listed, one of them tried once; 1,790 pages held in epoch 1, 40 more
 * written there and rewritten since by a pass.
 */
export function prerenderDemand(
  overrides: Partial<PrerenderDemandReport> = {},
): PrerenderDemandReport {
  return {
    generatedAt: '2026-10-21T20:00:00Z',
    isEnabled: true,
    isActive: true,
    isCapturing: true,
    queueSharePercent: 50,
    epoch: 1,
    pending: 412,
    rendering: 3,
    done: 1_830,
    failed: 2,
    skipped: 37,
    writtenLastHour: 64,
    writtenLastDay: 1_210,
    servedEpochPages: 1_790,
    servedEpochBytes: 179_000_000,
    superseded: 40,
    hours: demandHours(),
    top: [...TOP],
    ...overrides,
  };
}

// Two pages the queue wrote: the last one revisable and its own, the other frozen and written
// again since by a pass.
const DONE: readonly PrerenderDemandListedPage[] = [
  {
    path: '/fr/15.13.1/champions/Aatrox',
    version: '15.13.1',
    locale: 'fr',
    pageType: 'champions',
    state: 'done',
    demand: 48,
    attempts: 0,
    firstSeenAt: '2026-10-21T18:00:00Z',
    lastSeenAt: '2026-10-21T19:58:00Z',
    renderedAt: '2026-10-21T19:59:00Z',
    epoch: 1,
    bytes: 98_000,
    storedClass: 'revisable',
    supersededAt: null,
    reason: null,
  },
  {
    path: '/en/14.1.1/items/1036-long-sword',
    version: '14.1.1',
    locale: 'en',
    pageType: 'items',
    state: 'done',
    demand: 12,
    attempts: 1,
    firstSeenAt: '2026-10-21T17:00:00Z',
    lastSeenAt: '2026-10-21T19:40:00Z',
    renderedAt: '2026-10-21T19:45:00Z',
    epoch: 1,
    bytes: 87_000,
    storedClass: 'archived',
    supersededAt: '2026-10-21T19:50:00Z',
    reason: null,
  },
];

/** The first page of the queue's written pages: two of 1,830, the last written first. */
export function prerenderDemandPages(
  overrides: Partial<PrerenderDemandPages> = {},
): PrerenderDemandPages {
  return { state: 'done', page: 1, pageSize: 50, total: 1_830, pages: [...DONE], ...overrides };
}
