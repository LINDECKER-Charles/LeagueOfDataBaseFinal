import type { PublicApiReference } from '../../../core/api/generated/models/public-api-reference';
import { groupThousands } from './group-thousands';

const CENTS_PER_EURO = 100;
const YEARLY = 'year';
const PER_YEAR = 'api.portal.billing.per_year';
const PER_MONTH = 'api.portal.billing.per_month';

/** A row of the price list, its texts as translation keys and parameters. */
export interface PricingRow {
  /** Stable code of the offer, `free` for the free plan. */
  readonly code: string;
  readonly name: string;
  /** In euros. */
  readonly price: number;
  /** `per_month` or `per_year` of a subscription, null for a one-off price. */
  readonly period: string | null;
  readonly volume: string;
  /** The requests, grouped by thousands as the legacy list wrote them. */
  readonly count: string;
  readonly rate: number;
}

function freeRow(reference: PublicApiReference): PricingRow {
  return {
    code: 'free',
    name: 'api.plan.free',
    price: 0,
    period: null,
    volume: 'api.portal.billing.requests_month',
    count: groupThousands(reference.freePlan.monthlyQuota),
    rate: reference.freePlan.ratePerMinute,
  };
}

function packRows(reference: PublicApiReference): PricingRow[] {
  return reference.packs.map((pack) => ({
    code: pack.code,
    name: `api.pack.${pack.code}`,
    price: pack.priceCents / CENTS_PER_EURO,
    period: null,
    volume: 'api.portal.billing.requests_once',
    count: groupThousands(pack.requests),
    rate: reference.creditsRatePerMinute,
  }));
}

function planRows(reference: PublicApiReference): PricingRow[] {
  return reference.plans.map((plan) => ({
    code: plan.code,
    name: `api.plan.${plan.code}`,
    price: plan.priceCents / CENTS_PER_EURO,
    period: plan.interval === YEARLY ? PER_YEAR : PER_MONTH,
    volume: 'api.portal.billing.requests_month',
    count: groupThousands(plan.monthlyQuota),
    rate: plan.ratePerMinute,
  }));
}

/**
 * The price list of the documentation, as the legacy page drew it: the free plan, the
 * credit packs at the rate credits give, then the subscriptions.
 */
export function pricingRows(reference: PublicApiReference): PricingRow[] {
  return [freeRow(reference), ...packRows(reference), ...planRows(reference)];
}
