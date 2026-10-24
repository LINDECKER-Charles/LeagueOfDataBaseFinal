import type { RobotDetail } from '../../../../core/api/generated/models/robot-detail';
import type { RobotFigures } from '../../../../core/api/generated/models/robot-figures';
import type { RobotHitPage } from '../../../../core/api/generated/models/robot-hit-page';
import type { RobotHitView } from '../../../../core/api/generated/models/robot-hit-view';
import type { RobotReport } from '../../../../core/api/generated/models/robot-report';
import type { RobotSummary } from '../../../../core/api/generated/models/robot-summary';

function figures(changes: Partial<RobotFigures> = {}): RobotFigures {
  return {
    hits: 0,
    verified: 0,
    mismatch: 0,
    unresolved: 0,
    success: 0,
    redirects: 0,
    clientErrors: 0,
    serverErrors: 0,
    refused: 0,
    bytes: 0,
    meanMs: 0,
    slowHits: 0,
    ...changes,
  };
}

const GOOGLEBOT: RobotSummary = {
  key: 'googlebot',
  name: 'Googlebot',
  operator: 'Google',
  category: 'search',
  isCrawler: true,
  pct: 75,
  figures: figures({
    hits: 3_000,
    verified: 2_900,
    mismatch: 100,
    success: 2_800,
    serverErrors: 200,
    refused: 200,
    meanMs: 140,
  }),
  firstDay: '2026-10-01',
  lastDay: '2026-10-03',
  topSection: 'archive',
  networks: ['googlebot.com'],
};

const UNDECLARED: RobotSummary = {
  key: 'undeclared:example.net',
  name: 'Undeclared browser',
  operator: 'example.net',
  category: 'undeclared',
  isCrawler: true,
  pct: 25,
  figures: figures({ hits: 1_000, success: 1_000, meanMs: 900 }),
  firstDay: '2026-10-02',
  lastDay: '2026-10-02',
  topSection: 'detail',
  networks: ['example.net'],
};

const REPORT: RobotReport = {
  range: '30d',
  from: '2026-10-01',
  to: '2026-10-03',
  days: 3,
  hitsSince: '2026-09-04',
  totals: {
    figures: figures({ hits: 4_000, verified: 2_900, mismatch: 100, refused: 200, meanMs: 330 }),
    robots: 2,
    crawlerHits: 4_000,
  },
  dates: ['2026-10-01', '2026-10-02', '2026-10-03'],
  categorySeries: [
    { name: 'search', values: [1_000, 1_000, 1_000] },
    { name: 'undeclared', values: [0, 1_000, 0] },
  ],
  categories: [
    { category: 'search', hits: 3_000, robots: 1, pct: 75 },
    { category: 'undeclared', hits: 1_000, robots: 1, pct: 25 },
  ],
  robots: [GOOGLEBOT, UNDECLARED],
  statuses: [
    { name: '200', count: 3_800, pct: 95 },
    { name: '503', count: 200, pct: 5 },
  ],
  sections: [
    { name: 'archive', count: 2_500, pct: 62.5 },
    { name: 'detail', count: 1_500, pct: 37.5 },
  ],
  verdicts: [
    { name: 'verified', count: 2_900, pct: 72.5 },
    { name: 'unverifiable', count: 1_000, pct: 25 },
  ],
  networks: [{ domain: 'googlebot.com', hits: 2_900, robots: 1, pct: 72.5 }],
};

const HIT: RobotHitView = {
  at: '2026-10-03T08:15:42Z',
  robotKey: 'googlebot',
  robotName: 'Googlebot',
  operator: 'Google',
  category: 'search',
  verdict: 'mismatch',
  network: 'example.net',
  method: 'GET',
  target: '/fr/16.1.1/items',
  section: 'archive',
  status: 503,
  durationMs: 12,
  bytes: 512,
  cache: 'MISS',
};

/** A robots report of three days, Googlebot and an undeclared robot, changed by `changes`. */
export function robotReport(changes: Partial<RobotReport> = {}): RobotReport {
  return { ...REPORT, ...changes };
}

/** A hit of the journal, changed by `changes`. */
export function robotHit(changes: Partial<RobotHitView> = {}): RobotHitView {
  return { ...HIT, ...changes };
}

/** A page of the journal of `items`. */
export function robotHitPage(items: RobotHitView[], hasMore = false, page = 1): RobotHitPage {
  return { items, page, hasMore };
}

const DETAIL: RobotDetail = {
  range: '30d',
  from: REPORT.from,
  to: REPORT.to,
  hitsSince: REPORT.hitsSince,
  robot: GOOGLEBOT,
  verifyDomains: ['googlebot.com', 'google.com', 'googleusercontent.com'],
  dates: REPORT.dates,
  statusSeries: [
    { name: '2xx', values: [900, 1_000, 900] },
    { name: '3xx', values: [0, 0, 0] },
    { name: '4xx', values: [0, 0, 0] },
    { name: '5xx', values: [100, 0, 100] },
  ],
  statuses: REPORT.statuses,
  sections: REPORT.sections,
  verdicts: REPORT.verdicts,
  networks: REPORT.networks,
  agents: [
    {
      agent: 'Mozilla/5.0 (compatible; Googlebot/2.1)',
      hits: 3_000,
      firstSeen: '2026-10-01T00:00:00Z',
      lastSeen: '2026-10-03T08:15:42Z',
    },
  ],
  paths: [
    { target: '/fr/16.1.1/items', hits: 40, lastStatus: 503, lastAt: '2026-10-03T08:15:42Z' },
  ],
  recent: [HIT],
};

/** The detail of Googlebot over the report's three days, changed by `changes`. */
export function robotDetail(changes: Partial<RobotDetail> = {}): RobotDetail {
  return { ...DETAIL, ...changes };
}
