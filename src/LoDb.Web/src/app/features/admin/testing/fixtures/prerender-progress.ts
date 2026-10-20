import type { PrerenderPassProgress } from '../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderUnitView } from '../../../../core/api/generated/models/prerender-unit-view';

const LOCALES = [
  'ar',
  'cs',
  'de',
  'el',
  'en',
  'es',
  'fr',
  'hu',
  'id',
  'it',
  'ja',
  'ko',
  'pl',
  'pt',
  'ro',
  'ru',
  'th',
  'tr',
  'vi',
  'zh-hans',
  'zh-hant',
];

const RUNNING: PrerenderUnitView = {
  version: '16.18.1',
  locale: 'fr',
  state: 'running',
  cursor: 312,
  pagesTotal: 1_140,
  pagesWritten: 309,
  pagesSkipped: 3,
  pagesFailed: 0,
  bytesWritten: 12_051_000,
  startedAt: '2026-10-21T08:38:01+00:00',
  updatedAt: '2026-10-21T08:42:03+00:00',
  endedAt: null,
  failure: null,
};

const PROGRESS: PrerenderPassProgress = {
  generatedAt: '2026-10-21T08:42:05+00:00',
  pass: {
    id: 12,
    epoch: 2,
    state: 'running',
    scope: {
      versionCount: null,
      newestListed: null,
      oldestListed: null,
      oldest: null,
      newest: null,
      locales: null,
      pageTypes: null,
      priority: ['16.18.1', '16.12.1', '15.2.1'],
      skipComplete: true,
    },
    authorId: 1,
    authorName: 'charles',
    createdAt: '2026-10-18T08:00:00+00:00',
    finishedAt: null,
    activeSeconds: 274_800,
    pagesTotal: 1_610_320,
    pagesWritten: 1_512_300,
    pagesSkipped: 19_840,
    pagesFailed: 270,
    bytesWritten: 58_979_700_000,
    units: { total: 4_452, pending: 1_410, running: 1, completed: 3_024, failed: 17 },
    pagesPerMinute: 231.4,
    estimatedEndAt: '2026-10-23T14:00:00+00:00',
    pausedAt: null,
    pauseReason: null,
    pagesLeft: 613_590,
  },
  locales: LOCALES,
  isComplete: false,
  scopeVersions: null,
  versions: [
    {
      version: '16.18.1',
      rank: 0,
      cells: 'ccwcccr..............',
      pagesTotal: 7_980,
      pagesWritten: 7_140,
      pagesSkipped: 40,
      pagesFailed: 2,
      bytesWritten: 278_460_000,
      updatedAt: '2026-10-21T08:42:03+00:00',
    },
  ],
  running: [RUNNING],
  next: [
    { version: '16.18.1', locale: 'hu' },
    { version: '16.18.1', locale: 'id' },
  ],
  recent: [
    {
      ...RUNNING,
      locale: 'es',
      state: 'completed',
      cursor: 1_140,
      pagesWritten: 1_134,
      pagesSkipped: 6,
      bytesWritten: 44_226_000,
      startedAt: '2026-10-21T08:31:55+00:00',
      updatedAt: '2026-10-21T08:38:00+00:00',
      endedAt: '2026-10-21T08:38:00+00:00',
    },
  ],
  failures: [
    {
      ...RUNNING,
      locale: 'de',
      state: 'completed',
      cursor: 1_138,
      pagesTotal: 1_138,
      pagesWritten: 1_130,
      pagesSkipped: 6,
      pagesFailed: 2,
      bytesWritten: 44_070_000,
      startedAt: '2026-10-21T08:24:10+00:00',
      updatedAt: '2026-10-21T08:30:40+00:00',
      endedAt: '2026-10-21T08:30:40+00:00',
      failure: 'The SSR answered 500, not a page to store.',
    },
  ],
  unitsWithFailures: 17,
  pulse: {
    observedAt: '2026-10-21T08:42:03+00:00',
    isWorkerEnabled: true,
    ssrUnreachableSince: null,
    waiting: [
      {
        version: '16.3.1',
        reason: 'catalog-pending',
        since: '2026-10-21T08:39:00+00:00',
        givesUpAt: '2026-10-21T09:09:00+00:00',
      },
    ],
  },
};

/**
 * An incremental read of a pass on prod, three days in: pass 12 fills epoch 2 with every
 * older patch it does not hold yet, three priority versions first; 16.18.1 changed since the
 * last read, its French unit running at page 312 of 1,140; one version waits for its catalog.
 * `changes` replaces fields of the progress.
 */
export function prerenderProgress(
  changes: Partial<PrerenderPassProgress> = {},
): PrerenderPassProgress {
  return { ...PROGRESS, ...changes };
}
