import type { AnalyticsRank } from '../../../../core/api/generated/models/analytics-rank';
import type { AnalyticsReport } from '../../../../core/api/generated/models/analytics-report';

// The seven days of a week, each of 24 hours.
const WEEK = 7;
const DAY_HOURS = 24;

function rank(name: string, count: number, pct = 0): AnalyticsRank {
  return { name, count, pct };
}

const REPORT: AnalyticsReport = {
  range: '30d',
  geoAvailable: true,
  days: 3,
  from: '2026-09-01',
  to: '2026-09-03',
  totals: {
    views: 12_340,
    uniqueVisitors: 2_100,
    returningVisitors: 640,
    botViews: 310,
    pagesTracked: 88,
  },
  series: [
    { date: '2026-09-01', views: 4_000, visitors: 700, botViews: 100 },
    { date: '2026-09-02', views: 3_340, visitors: 600, botViews: 90 },
    { date: '2026-09-03', views: 5_000, visitors: 800, botViews: 120 },
  ],
  heatmap: Array.from({ length: WEEK }, (_, day) =>
    Array.from({ length: DAY_HOURS }, (_, hour) => (day === 1 && hour === 20 ? 90 : 0)),
  ),
  byHour: [],
  byWeekday: [],
  topPages: [rank('/fr/champions', 900), rank('/en/items', 450)],
  topEntities: [rank('champion:Ahri', 300)],
  byType: [rank('champion', 600, 60), rank('item', 400, 40)],
  byKind: [rank('page', 1_000)],
  status: [rank('200', 12_000), rank('404', 340)],
  byRoute: [rank('champion-detail', 800)],
  device: [rank('mobile', 700, 70), rank('desktop', 300, 30)],
  refSource: [rank('search', 500, 50), rank('direct', 500, 50)],
  browser: [rank('Firefox', 420)],
  os: [rank('Linux', 210)],
  country: [{ code: 'FR', name: 'France', count: 900, pct: 90 }],
  topReferers: [rank('duckduckgo.com', 120)],
  lang: [rank('fr', 800)],
  locale: [rank('fr-FR', 700)],
};

/** An analytics report of three days, every ranking filled, changed by `changes`. */
export function analyticsReport(changes: Partial<AnalyticsReport> = {}): AnalyticsReport {
  return { ...REPORT, ...changes };
}
