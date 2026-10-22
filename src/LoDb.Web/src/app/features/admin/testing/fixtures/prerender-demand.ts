import type { PrerenderDemandPage } from '../../../../core/api/generated/models/prerender-demand-page';
import type { PrerenderDemandReport } from '../../../../core/api/generated/models/prerender-demand-report';

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
 * The automatic queue as the API reports it: active, reading the SSR, 412 pages pending, the
 * two most requested listed, one of them tried once.
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
    done: 1_830,
    failed: 2,
    skipped: 37,
    writtenLastHour: 64,
    writtenLastDay: 1_210,
    top: [...TOP],
    ...overrides,
  };
}
