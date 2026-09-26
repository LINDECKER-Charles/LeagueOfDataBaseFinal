import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-base-url';
import { getDonationOptions } from '../../core/api/generated/fn/donations/get-donation-options';
import type { DonationOptions } from '../../core/api/generated/models/donation-options';
import { PageResponse } from '../../core/routing/response/page-response';

/** The form as the API closes it: the legacy tiers and bounds, nothing to send. */
export const CLOSED_DONATIONS: DonationOptions = {
  available: false,
  currency: 'eur',
  presets: [300, 500, 1_000, 2_500],
  minCents: 100,
  maxCents: 50_000,
};

function isCents(value: unknown): value is number {
  return Number.isSafeInteger(value) && (value as number) > 0;
}

// The answer is read before it is trusted: a proxy page or an older API leaves the form closed.
function isDonationOptions(value: unknown): value is DonationOptions {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const options = value as Partial<Record<keyof DonationOptions, unknown>>;
  return (
    typeof options.available === 'boolean' &&
    typeof options.currency === 'string' &&
    Array.isArray(options.presets) &&
    options.presets.length > 0 &&
    options.presets.every(isCents) &&
    isCents(options.minCents) &&
    isCents(options.maxCents)
  );
}

/**
 * Resolver of the donation form (`options`): its tiers, its bounds, and whether Stripe is
 * set up behind it. Never fails: without an answer the page still renders, its form closed.
 * The answer follows the API's configuration, not the patch, so the proxy keeps it a minute.
 */
export const resolveDonationOptions: ResolveFn<DonationOptions> = async () => {
  const http = inject(HttpClient);
  const apiOrigin = inject(API_BASE_URL);
  inject(PageResponse).cache('transient');
  try {
    const answer = await firstValueFrom(getDonationOptions(http, apiOrigin));
    return isDonationOptions(answer.body) ? answer.body : CLOSED_DONATIONS;
  } catch {
    return CLOSED_DONATIONS;
  }
};
