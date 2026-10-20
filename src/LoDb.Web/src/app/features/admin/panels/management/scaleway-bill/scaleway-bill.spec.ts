import type { ScalewayBillPeriod } from '../../../../../core/api/generated/models/scaleway-bill-period';
import type { ScalewayBillReport } from '../../../../../core/api/generated/models/scaleway-bill-report';
import { billAmount } from './bill-amount';
import { billMonth } from './bill-month';
import { billQuantity } from './bill-quantity';
import { billViewOf } from './bill-view';

const STORAGE = {
  product: 'Standard Multi-AZ',
  resource: 'Multi-AZ - fr-par',
  quantity: 196,
  unit: 'gigabyte_hour',
  amount: 0.01,
};

/** Six months, newest first, those at `billed` with a line. */
function months(...billed: number[]): ScalewayBillPeriod[] {
  return ['2026-10', '2026-09', '2026-08', '2026-07', '2026-06', '2026-05'].map((period, index) =>
    billed.includes(index)
      ? { period, total: 0.01, lines: [STORAGE] }
      : { period, total: 0, lines: [] },
  );
}

function report(changes: Partial<ScalewayBillReport> = {}): ScalewayBillReport {
  return {
    configured: true,
    stale: false,
    fetchedAt: '2026-10-20T18:00:00Z',
    currency: 'EUR',
    backups: { projectId: 'backups', periods: months(0, 1) },
    pages: { projectId: 'pages', periods: months() },
    ...changes,
  };
}

describe('billViewOf', () => {
  it('leaves out the months before the first one billed, and keeps an empty one after it', () => {
    const view = billViewOf(
      report({ backups: { projectId: 'b', periods: months(0, 2) } }),
      'backups',
    );

    expect(view.state).toBe('billed');
    expect(view.months.map((month) => month.period)).toEqual(['2026-10', '2026-09', '2026-08']);
  });

  it('keeps the current month when only an older one was billed', () => {
    const view = billViewOf(report({ backups: { projectId: 'b', periods: months(1) } }), 'backups');

    expect(view.months.map((month) => [month.period, month.lines.length])).toEqual([
      ['2026-10', 0],
      ['2026-09', 1],
    ]);
  });

  it('reads a project that consumed nothing over the six months as empty', () => {
    expect(billViewOf(report(), 'pages')).toMatchObject({ state: 'empty', months: [] });
  });

  it('reads an unset key as not configured, whatever else the report says', () => {
    const view = billViewOf({ configured: false, stale: true }, 'backups');

    expect(view).toMatchObject({ state: 'not_configured', isStale: false, months: [] });
  });

  it('reads a report without a bill as unavailable, its last reading kept if any', () => {
    const none = { configured: true, stale: true, backups: null, pages: null };

    expect(billViewOf(none, 'backups')).toMatchObject({
      state: 'unavailable',
      isStale: true,
      fetchedAt: null,
      currency: 'EUR',
    });
  });

  it('keeps a stale bill, with the time of its reading', () => {
    const view = billViewOf(report({ stale: true }), 'backups');

    expect(view).toMatchObject({
      state: 'billed',
      isStale: true,
      fetchedAt: '2026-10-20T18:00:00Z',
    });
  });
});

describe('the bill formats', () => {
  it('writes an amount to its last significant decimal, a deduction negative', () => {
    expect(billAmount(0.0517, 'EUR')).toBe('0,0517 €');
    expect(billAmount(1234.5, 'EUR')).toBe('1 234,50 €');
    expect(billAmount(-0.01, 'EUR')).toBe('-0,01 €');
    expect(billAmount(-0, 'EUR')).toBe('0,00 €');
  });

  it('names a period by its month, in French', () => {
    expect(billMonth('2026-10')).toBe('octobre 2026');
    expect(billMonth('2026-01')).toBe('janvier 2026');
    expect(billMonth('soon')).toBe('soon');
  });

  it('writes a quantity with spaced thousands, a deduction negative', () => {
    expect(billQuantity(3220)).toBe('3 220');
    expect(billQuantity(0.5)).toBe('0,5');
    expect(billQuantity(-1)).toBe('-1');
  });
});
