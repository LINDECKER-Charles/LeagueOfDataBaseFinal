import type { ScalewayBillPeriod } from '../../../../../core/api/generated/models/scaleway-bill-period';
import type { ScalewayBillReport } from '../../../../../core/api/generated/models/scaleway-bill-report';
import type { ScalewayProjectBill } from '../../../../../core/api/generated/models/scaleway-project-bill';

/** The Scaleway project a panel shows the bill of: the backups', or the page store's. */
export type BillProject = 'backups' | 'pages';

/**
 * What the bill block says: that the billing key is not set, that Scaleway has not answered
 * since the API started, that the project consumed nothing over the six months, or the bill.
 */
export type BillState = 'not_configured' | 'unavailable' | 'empty' | 'billed';

/** The bill of one project, as its block shows it. */
export interface BillView {
  readonly state: BillState;
  /** Whether the last reading failed: what shows, if anything, is the one before. */
  readonly isStale: boolean;
  /** When the bill shown was read from Scaleway, null before any good reading. */
  readonly fetchedAt: string | null;
  readonly currency: string;
  /** The months from the first one billed to the current one, newest first. */
  readonly months: readonly ScalewayBillPeriod[];
}

// The API leaves the currency out before any good reading: nothing is shown in it then.
const DEFAULT_CURRENCY = 'EUR';

/** The months of `periods` (newest first) down to the oldest one with a line. */
function billedMonths(periods: readonly ScalewayBillPeriod[]): readonly ScalewayBillPeriod[] {
  let oldest = periods.length - 1;
  while (oldest >= 0 && periods[oldest]?.lines.length === 0) {
    oldest--;
  }
  return periods.slice(0, oldest + 1);
}

function stateOf(
  isConfigured: boolean,
  bill: ScalewayProjectBill | null,
  months: readonly ScalewayBillPeriod[],
): BillState {
  if (!isConfigured) {
    return 'not_configured';
  }
  if (!bill) {
    return 'unavailable';
  }
  return months.length === 0 ? 'empty' : 'billed';
}

/** The bill of `project` in `report`, the months before its first consumption left out. */
export function billViewOf(report: ScalewayBillReport, project: BillProject): BillView {
  const bill = report[project] ?? null;
  const months = bill ? billedMonths(bill.periods) : [];
  return {
    state: stateOf(report.configured, bill, months),
    isStale: report.configured && report.stale,
    fetchedAt: report.fetchedAt ?? null,
    currency: report.currency ?? DEFAULT_CURRENCY,
    months,
  };
}
