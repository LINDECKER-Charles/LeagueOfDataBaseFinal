import type { ScalewayBillLine } from '../../../../core/api/generated/models/scaleway-bill-line';
import type { ScalewayBillReport } from '../../../../core/api/generated/models/scaleway-bill-report';
import type { ScalewayProjectBill } from '../../../../core/api/generated/models/scaleway-project-bill';

// The six periods the API sends, newest first: October 2026 is the current month.
const PERIODS = ['2026-10', '2026-09', '2026-08', '2026-07', '2026-06', '2026-05'];
// Obvious fakes: no real Scaleway id belongs in a spec.
const BACKUPS_PROJECT = '00000000-0000-4000-8000-00000000000b';
const PAGES_PROJECT = '00000000-0000-4000-8000-00000000000c';
// The API rounds every amount to four decimals.
const TEN_THOUSANDTHS = 10_000;

function line(
  product: string,
  resource: string,
  billed: Pick<ScalewayBillLine, 'quantity' | 'unit' | 'amount'>,
): ScalewayBillLine {
  return { product, resource, ...billed };
}

/** The lines of a new project in its first month, as Scaleway answered: a free trial's. */
const FIRST_MONTH: readonly ScalewayBillLine[] = [
  line('Standard Multi-AZ', 'Multi-AZ - fr-par', {
    quantity: 196,
    unit: 'gigabyte_hour',
    amount: 0.01,
  }),
  line('External Bandwidth', 'fr-par - egress internet', {
    quantity: 1,
    unit: 'gigabyte',
    amount: 0.01,
  }),
  line('Multi-AZ Standard free-trial', 'Pack', { quantity: 1, unit: 'plan', amount: 0 }),
  line('Multi-AZ Standard free-trial', 'Offer deducted - Object Storage Free Trial', {
    quantity: -1,
    unit: 'currency',
    amount: -0.01,
  }),
  line('Outgoing bandwidth free tier', 'Offer deducted - External Bandwidth Free Tier', {
    quantity: -1,
    unit: 'currency',
    amount: -0.01,
  }),
];

/** A month past the free trial: the storage alone. */
const PAID_MONTH: readonly ScalewayBillLine[] = [
  line('Standard Multi-AZ', 'Multi-AZ - fr-par', {
    quantity: 3220,
    unit: 'gigabyte_hour',
    amount: 0.0517,
  }),
];

/**
 * The bill of a project whose months, newest first, hold `billed` (the older ones nothing),
 * each total the sum of its lines as the API sends it.
 */
function projectBill(
  projectId: string,
  billed: readonly (readonly ScalewayBillLine[])[],
): ScalewayProjectBill {
  return {
    projectId,
    periods: PERIODS.map((period, index) => {
      const lines = [...(billed[index] ?? [])];
      const total = lines.reduce((sum, row) => sum + row.amount, 0);
      return { period, total: Math.round(total * TEN_THOUSANDTHS) / TEN_THOUSANDTHS, lines };
    }),
  };
}

/**
 * A bill read at 18:00 on 20 October 2026: the backups billed since September (October still
 * in its free trial), the pages since October.
 */
export function scalewayBillReport(changes: Partial<ScalewayBillReport> = {}): ScalewayBillReport {
  return {
    configured: true,
    stale: false,
    fetchedAt: '2026-10-20T18:00:00Z',
    currency: 'EUR',
    backups: projectBill(BACKUPS_PROJECT, [FIRST_MONTH, PAID_MONTH]),
    pages: projectBill(PAGES_PROJECT, [FIRST_MONTH]),
    ...changes,
  };
}
