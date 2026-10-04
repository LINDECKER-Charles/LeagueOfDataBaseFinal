import type { PortalNotice } from './portal-notice';

// The statuses Stripe's return URLs carry (Billing/Checkout/CheckoutPages).
const STATUSES = ['pack_success', 'plan_success', 'cancelled'] as const;

type CheckoutStatus = (typeof STATUSES)[number];

function isCheckoutStatus(value: string): value is CheckoutStatus {
  return (STATUSES as readonly string[]).includes(value);
}

/**
 * The banner of a return from Stripe (`?status=`), from an allowlist: the parameter never
 * names an arbitrary key. The entitlements land by webhook, a few seconds later.
 */
export function checkoutNotice(status: string | null): PortalNotice | null {
  if (status === null || !isCheckoutStatus(status)) {
    return null;
  }
  return {
    tone: status === 'cancelled' ? 'muted' : 'success',
    key: `api.portal.status.${status}`,
  };
}
