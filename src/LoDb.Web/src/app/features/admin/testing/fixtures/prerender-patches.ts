import type { PrerenderPatchesReport } from '../../../../core/api/generated/models/prerender-patches-report';

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

const REPORT: PrerenderPatchesReport = {
  generatedAt: '2026-10-21T08:40:00+00:00',
  epoch: 2,
  servedEpoch: 2,
  latestVersion: '16.20.1',
  locales: LOCALES,
  patches: [
    {
      version: '16.19.1',
      cells: 'ccccccccccccccccccccc',
      types: 'fffffffffffffffffffff',
      completeLocales: 21,
      pages: 24_310,
      bytes: 948_090_000,
      failedPages: 0,
      pagesPerLocale: 1_158,
      lastEndedAt: '2026-10-20T14:02:11+00:00',
    },
    {
      version: '16.18.1',
      cells: 'ccccwcpf-------------',
      types: 'ffffdf200000000000000',
      completeLocales: 5,
      pages: 6_140,
      bytes: 239_460_000,
      failedPages: 2,
      pagesPerLocale: 1_140,
      lastEndedAt: '2026-10-21T08:30:40+00:00',
    },
  ],
  coverage: {
    pinnedVersions: 402,
    expectedUnits: 8_442,
    completeUnits: 8_274,
    cleanUnits: 8_223,
    failedUnits: 12,
    gapUnits: 156,
    failedPages: 270,
    completeVersions: 391,
    gapVersions: 11,
  },
  estimates: {
    typeShares: { champions: 0.1512, items: 0.5803, runes: 0.1894, summoners: 0.0791 },
    bytesPerPage: 39_000,
    measuredPagesPerMinute: 112.6,
    configuredRendersPerMinute: 120,
    pricePerGbMonthOneZone: 0.00803,
    currency: 'EUR',
  },
};

/**
 * The older patches and what the served epoch 2 holds of them, cut to two patches: 16.19.1
 * whole, 16.18.1 under way (five locales complete, one with failed pages, one partial holding
 * only the items, one failed for good, the rest absent); the summary is of all 402. `changes`
 * replaces fields of the report.
 */
export function prerenderPatches(
  changes: Partial<PrerenderPatchesReport> = {},
): PrerenderPatchesReport {
  return { ...REPORT, ...changes };
}
