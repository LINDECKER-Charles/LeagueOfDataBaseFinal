import type { PrerenderEpochCoverage } from '../../../../core/api/generated/models/prerender-epoch-coverage';
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
  pausedAt: null,
  pauseReason: null,
  // 28,770 pages of the listed units, plus the 59 pending ones at the listed mean.
  pagesLeft: 75_658,
  scope: {
    versionCount: null,
    newestListed: null,
    oldestListed: null,
    oldest: null,
    newest: null,
    locales: null,
    pageTypes: null,
    priority: ['16.19.1', '16.18.1'],
    skipComplete: false,
  },
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
  pagesLeft: 0,
  // Launched before the lists: a range of versions, one type, one locale.
  scope: {
    versionCount: null,
    newestListed: null,
    oldestListed: null,
    oldest: '14.1.1',
    newest: '14.24.1',
    locales: ['fr'],
    pageTypes: ['items'],
    priority: [],
    skipComplete: false,
  },
  units: { total: 5, completed: 5, running: 0, pending: 0, failed: 0 },
};

// What epoch 2 holds of the older patches: the example of the patches report.
const COVERAGE: PrerenderEpochCoverage = {
  pinnedVersions: 402,
  expectedUnits: 8_442,
  completeUnits: 8_274,
  cleanUnits: 8_223,
  failedUnits: 12,
  gapUnits: 156,
  failedPages: 270,
  completeVersions: 391,
  gapVersions: 11,
};

const REPORT: PrerenderReport = {
  generatedAt: '2026-10-20T12:00:00Z',
  isConfigured: true,
  enqueuesPromotions: true,
  servedEpoch: 2,
  readyEpoch: null,
  current: RUNNING,
  passes: [RUNNING, CANCELLED],
  storeBucket: 'lodb-pages-prod',
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
        coverage: COVERAGE,
      },
      {
        epoch: 1,
        state: 'retired',
        isWriting: false,
        pages: 2_400,
        bytes: 0.24 * GB,
        monthlyCost: 0.0019,
        coverage: null,
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
 * its first two days with two priority versions, 156 (version, locale) still missing; epoch 1,
 * which a small pass on French items wrote before it was cancelled, retired and awaiting its
 * deletion. `changes` replaces fields of the report.
 */
export function prerenderReport(changes: Partial<PrerenderReport> = {}): PrerenderReport {
  return { ...REPORT, ...changes };
}
