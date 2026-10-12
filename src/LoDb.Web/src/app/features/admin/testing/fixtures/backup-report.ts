import type { BackupReport } from '../../../../core/api/generated/models/backup-report';
import type { BackupRunRow } from '../../../../core/api/generated/models/backup-run-row';

const NIGHT = '2026-10-12';

function run(id: number, changes: Partial<BackupRunRow>): BackupRunRow {
  return {
    id,
    step: 'backup',
    startedAt: `${NIGHT}T02:00:00Z`,
    finishedAt: `${NIGHT}T02:01:12Z`,
    succeeded: true,
    ...changes,
  };
}

const REPORT: BackupReport = {
  generatedAt: `${NIGHT}T09:00:00Z`,
  days: 7,
  databases: [
    {
      name: 'lodb',
      status: 'ok',
      lastSuccessAt: `${NIGHT}T02:01:12Z`,
      lastBytesRead: 512 * 1024 ** 2,
      restoreStatus: 'ok',
      lastRestoreAt: '2026-10-01T02:20:00Z',
    },
    {
      name: 'umami',
      status: 'failed',
      lastSuccessAt: '2026-10-11T02:03:00Z',
      lastFailureAt: `${NIGHT}T03:02:00Z`,
      lastBytesRead: 40 * 1024 ** 2,
    },
  ],
  repository: {
    bytes: 3 * 1024 ** 3,
    measuredAt: `${NIGHT}T03:05:00Z`,
    pricePerGbMonth: 0.01606,
    currency: 'EUR',
    monthlyCost: 0.0517,
  },
  runs: [
    run(5, {
      step: 'retention',
      startedAt: `${NIGHT}T03:04:00Z`,
      finishedAt: `${NIGHT}T03:05:00Z`,
      repositoryBytes: 3 * 1024 ** 3,
    }),
    run(4, {
      database: 'umami',
      startedAt: `${NIGHT}T03:01:00Z`,
      finishedAt: `${NIGHT}T03:02:00Z`,
      succeeded: false,
      detail: 'connection to server at "analytics-postgres" failed',
    }),
    run(3, { database: 'lodb', bytesRead: 512 * 1024 ** 2, bytesAdded: 3 * 1024 ** 2 }),
    run(2, {
      database: 'umami',
      startedAt: '2026-10-11T02:02:00Z',
      finishedAt: '2026-10-11T02:03:00Z',
      bytesRead: 40 * 1024 ** 2,
      bytesAdded: 1024 ** 2,
    }),
    run(1, {
      database: 'lodb',
      startedAt: '2026-10-11T02:00:00Z',
      finishedAt: '2026-10-11T02:01:00Z',
      bytesRead: 510 * 1024 ** 2,
      bytesAdded: 2 * 1024 ** 2,
    }),
  ],
};

/**
 * A backups report over a week: LoDb up to date and restore-tested, Umami failing since its
 * last night, the repository measured, the steps of the last two nights.
 */
export function backupReport(changes: Partial<BackupReport> = {}): BackupReport {
  return { ...REPORT, ...changes };
}
