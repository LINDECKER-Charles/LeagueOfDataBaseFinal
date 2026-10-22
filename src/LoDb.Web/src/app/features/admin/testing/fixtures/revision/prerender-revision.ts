import type { PrerenderRevisionReport } from '../../../../../core/api/generated/models/prerender-revision-report';

/**
 * The queue of the closed states as the API reports it: active, 1 302 pages pending on the
 * served epoch 2 and 504 on the epoch 3 a pass fills, 210 of them due, the next due at 20:05.
 */
export function prerenderRevision(
  overrides: Partial<PrerenderRevisionReport> = {},
): PrerenderRevisionReport {
  return {
    generatedAt: '2026-10-21T20:00:00Z',
    isActive: true,
    servedEpoch: 2,
    pending: 1_806,
    due: 210,
    done: 22_430,
    failed: 3,
    skipped: 12,
    writtenLastHour: 840,
    writtenLastDay: 22_430,
    nextDueAt: '2026-10-21T20:05:00Z',
    closedThrough: '2026-10-21T19:43:00Z',
    epochs: [
      { epoch: 3, pending: 504, done: 0, failed: 0, skipped: 0 },
      { epoch: 2, pending: 1_302, done: 22_430, failed: 3, skipped: 12 },
    ],
    ...overrides,
  };
}
