import type { PrerenderPassView } from '../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../core/api/generated/models/prerender-report';

const GB = 1_000_000_000;

const RUNNING: PrerenderPassView = {
  id: 3,
  epoch: 2,
  state: 'running',
  createdAt: '2026-10-18T08:00:00Z',
  finishedAt: null,
  authorId: 1,
  authorName: 'charles',
  activeSeconds: 183_600,
  pagesTotal: 120_000,
  pagesWritten: 90_000,
  pagesSkipped: 1_200,
  pagesFailed: 30,
  bytesWritten: 9 * GB,
  pagesPerMinute: 210.5,
  estimatedEndAt: '2026-10-21T02:00:00Z',
  scope: { priority: ['16.19.1', '16.18.1'] },
  units: { total: 210, completed: 150, running: 1, pending: 59, failed: 0 },
};

const CANCELLED: PrerenderPassView = {
  ...RUNNING,
  id: 2,
  epoch: 1,
  state: 'cancelled',
  createdAt: '2026-10-15T08:00:00Z',
  finishedAt: '2026-10-15T09:00:00Z',
  authorId: null,
  authorName: null,
  activeSeconds: 3_600,
  pagesTotal: 2_500,
  pagesWritten: 2_400,
  pagesSkipped: 100,
  pagesFailed: 0,
  bytesWritten: 0.24 * GB,
  pagesPerMinute: 41.7,
  estimatedEndAt: null,
  scope: { oldest: '14.1.1', newest: '14.24.1', pageType: 'items', locale: 'fr', priority: [] },
  units: { total: 5, completed: 5, running: 0, pending: 0, failed: 0 },
};

const REPORT: PrerenderReport = {
  generatedAt: '2026-10-20T12:00:00Z',
  isConfigured: true,
  enqueuesPromotions: true,
  servedEpoch: 2,
  readyEpoch: null,
  current: RUNNING,
  passes: [RUNNING, CANCELLED],
  storage: {
    pages: 91_230,
    bytes: 9.264 * GB,
    monthlyCost: 0.0744,
    currency: 'EUR',
    excludesEgress: true,
    pricePerGbMonthOneZone: 0.00803,
    pricePerGbMonthMultiAz: 0.01606,
    epochs: [
      {
        epoch: 2,
        state: 'served',
        isWriting: true,
        pages: 91_230,
        bytes: 9.24 * GB,
        monthlyCost: 0.0742,
      },
      {
        epoch: 1,
        state: 'retired',
        isWriting: false,
        pages: 2_400,
        bytes: 0.24 * GB,
        monthlyCost: 0.0019,
      },
    ],
    builds: { count: 2, files: 184, bytes: 0.024 * GB, monthlyCost: 0.0004 },
    servedVersions: [
      { version: '16.19.1', locales: 21, pages: 24_000, bytes: 2.4 * GB },
      { version: '16.18.1', locales: 12, pages: 13_000, bytes: 1.3 * GB },
    ],
  },
};

/**
 * A report of the pre-rendering on prod: epoch 2 served while a pass fills it, running past
 * its first two days with two priority versions; epoch 1, which a small pass on French items
 * wrote before it was cancelled, retired and awaiting its deletion. `changes` replaces fields
 * of the report.
 */
export function prerenderReport(changes: Partial<PrerenderReport> = {}): PrerenderReport {
  return { ...REPORT, ...changes };
}
