import type { BackupRunRow } from '../../../../../core/api/generated/models/backup-run-row';

/**
 * How a night of backups went: every step through at the first attempt (`ok`), through after
 * a retry (`recovered`), a step whose last attempt failed (`failed`), or no pass at all.
 */
export type NightOutcome = 'ok' | 'recovered' | 'failed' | 'none';

/** A UTC day of the window and how its pass went. */
export interface BackupNight {
  /** The UTC day, `2026-10-12`. */
  readonly day: string;
  readonly outcome: NightOutcome;
  /** The steps the pass ran that day, retries included. */
  readonly steps: number;
}

const DAY_MS = 86_400_000;
// `2026-10-12T02:00:00.000Z`: the day ends at 10.
const ISO_DAY_END = 10;

function dayOf(iso: string): string {
  return new Date(iso).toISOString().slice(0, ISO_DAY_END);
}

function byDay(runs: readonly BackupRunRow[]): Map<string, BackupRunRow[]> {
  const days = new Map<string, BackupRunRow[]>();
  for (const run of runs) {
    const day = dayOf(run.startedAt);
    days.set(day, [...(days.get(day) ?? []), run]);
  }
  return days;
}

// The last attempt of each step of each database decides; a failure it retried is only noted.
function outcomeOf(runs: readonly BackupRunRow[]): NightOutcome {
  if (runs.length === 0) {
    return 'none';
  }
  const last = new Map<string, BackupRunRow>();
  const oldestFirst = [...runs].sort((a, b) => Date.parse(a.startedAt) - Date.parse(b.startedAt));
  for (const run of oldestFirst) {
    last.set(`${run.step}:${run.database ?? ''}`, run);
  }
  if ([...last.values()].some((run) => !run.succeeded)) {
    return 'failed';
  }
  return runs.some((run) => !run.succeeded) ? 'recovered' : 'ok';
}

/**
 * The `days` UTC days up to the day of `until`, oldest first, each told by the steps that
 * started on it: the passes start at night and retry within the same day.
 */
export function backupNights(
  runs: readonly BackupRunRow[],
  days: number,
  until: string,
): BackupNight[] {
  const steps = byDay(runs);
  const last = Date.parse(until);
  return Array.from({ length: days }, (_, index) => {
    const day = dayOf(new Date(last - (days - 1 - index) * DAY_MS).toISOString());
    const ran = steps.get(day) ?? [];
    return { day, outcome: outcomeOf(ran), steps: ran.length };
  });
}
