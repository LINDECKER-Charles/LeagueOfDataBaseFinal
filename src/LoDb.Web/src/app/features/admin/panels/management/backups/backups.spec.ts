import type { BackupRunRow } from '../../../../../core/api/generated/models/backup-run-row';
import { backupNights } from './backup-nights';
import { backupTone } from './backup-tone';
import { monthlyCost } from './monthly-cost';
import { unitPrice } from './unit-price';

let ids = 0;

function step(startedAt: string, changes: Partial<BackupRunRow> = {}): BackupRunRow {
  return {
    id: ++ids,
    step: 'backup',
    database: 'lodb',
    startedAt,
    finishedAt: startedAt,
    succeeded: true,
    ...changes,
  };
}

describe('backupNights', () => {
  it('gives every day of the window, oldest first, a day without a pass as none', () => {
    const nights = backupNights([step('2026-10-12T02:00:00Z')], 3, '2026-10-12T09:00:00Z');

    expect(nights).toEqual([
      { day: '2026-10-10', outcome: 'none', steps: 0 },
      { day: '2026-10-11', outcome: 'none', steps: 0 },
      { day: '2026-10-12', outcome: 'ok', steps: 1 },
    ]);
  });

  it('tells a night by the last attempt of each step of each database', () => {
    const nights = backupNights(
      [
        step('2026-10-11T02:00:00Z', { succeeded: false }),
        step('2026-10-11T03:00:00Z'),
        step('2026-10-12T02:00:00Z'),
        step('2026-10-12T02:01:00Z', { database: 'umami', succeeded: false }),
        step('2026-10-12T02:02:00Z', { step: 'retention', database: null }),
      ],
      2,
      '2026-10-12T09:00:00Z',
    );

    expect(nights.map((night) => night.outcome)).toEqual(['recovered', 'failed']);
  });
});

describe('the backups formats', () => {
  it('writes a cost to the cent, never a real one as nothing', () => {
    expect(monthlyCost(0.0517, 'EUR')).toBe('0,05 €');
    expect(monthlyCost(1234.5, 'EUR')).toBe('1 234,50 €');
    expect(monthlyCost(0.0004, 'EUR')).toBe('< 0,01 €');
    expect(monthlyCost(0, 'EUR')).toBe('0,00 €');
  });

  it('writes a price per GB to its last significant decimal', () => {
    expect(unitPrice(0.01606, 'EUR')).toBe('0,01606 €');
    expect(unitPrice(0.02, 'EUR')).toBe('0,02 €');
  });

  it('reads a state it does not know as a failure', () => {
    expect(['ok', 'late', 'recovered', 'none', 'unknown'].map(backupTone)).toEqual([
      'good',
      'warn',
      'warn',
      'muted',
      'bad',
    ]);
  });
});
