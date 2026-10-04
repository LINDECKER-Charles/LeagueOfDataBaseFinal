import type { MonitoringReport } from '../../../../core/api/generated/models/monitoring-report';

const REPORT: MonitoringReport = {
  generatedAt: '2026-09-27T10:15:30Z',
  services: [
    {
      name: 'postgres',
      status: 'ok',
      latencyMs: 4,
      version: '17.2',
      databaseBytes: 3 * 1024 ** 3,
    },
    { name: 'storage', status: 'degraded', latencyMs: 812, detail: 'slow listing' },
  ],
  process: {
    version: '1.4.0',
    revision: 'abc1234',
    uptimeSeconds: 2 * 86_400 + 3 * 3_600,
    workingSetBytes: 256 * 1024 ** 2,
    managedHeapBytes: 64 * 1024 ** 2,
    threadPoolThreads: 12,
    pendingWorkItems: 0,
    cpuSeconds: 431,
    collections: [120, 14, 2],
  },
  ingestion: { versionBacklog: 1, onDemandBacklog: 0, outboxPending: 3, outboxDead: 1 },
  tables: [
    { name: 'analytics_daily', bytes: 80 * 1024 ** 2 },
    { name: 'audit_log', bytes: 20 * 1024 ** 2 },
  ],
  versions: {
    current: '16.19.1',
    promotedAt: '2026-09-24T08:00:00Z',
    ready: 42,
    ingesting: 1,
    failed: 1,
    discovered: 0,
    recent: [
      { version: '16.20.1', status: 'ingesting', attempts: 1, updatedAt: '2026-09-27T09:00:00Z' },
      { version: '16.19.1', status: 'ready', attempts: 1, updatedAt: '2026-09-24T08:00:00Z' },
    ],
  },
  counters: {
    usersTotal: 5_400,
    usersNewWeek: 37,
    usersBanned: 2,
    buildsTotal: 9_800,
    buildsPublic: 3_100,
    votes: 12_000,
    apiKeysActive: 14,
    apiRequestsToday: 4_200,
    apiRequestsMonth: 98_000,
    donationsCount: 25,
    donationsTotalCents: 31_500,
  },
};

/** A monitoring report: PostgreSQL up, the storage slow, every section filled. */
export function monitoringReport(changes: Partial<MonitoringReport> = {}): MonitoringReport {
  return { ...REPORT, ...changes };
}
