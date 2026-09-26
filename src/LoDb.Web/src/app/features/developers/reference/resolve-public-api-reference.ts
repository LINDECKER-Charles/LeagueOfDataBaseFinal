import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import { getPublicApiReference } from '../../../core/api/generated/fn/public-api-reference/get-public-api-reference';
import type { PublicApiReference } from '../../../core/api/generated/models/public-api-reference';
import { PageResponse } from '../../../core/routing/response/page-response';

function isCount(value: unknown): value is number {
  return Number.isSafeInteger(value) && (value as number) >= 0;
}

function isWebOrigin(value: unknown): value is string {
  return typeof value === 'string' && /^https?:\/\//.test(value) && URL.canParse(value);
}

function isFreePlan(value: unknown): boolean {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const free = value as Partial<Record<string, unknown>>;
  return isCount(free['monthlyQuota']) && isCount(free['ratePerMinute']);
}

// The answer is read before it is trusted: a proxy page or an older API documents nothing.
function isReference(value: unknown): value is PublicApiReference {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const reference = value as Partial<Record<keyof PublicApiReference, unknown>>;
  return (
    isWebOrigin(reference.baseUrl) &&
    typeof reference.keyPrefix === 'string' &&
    isFreePlan(reference.freePlan) &&
    isCount(reference.creditsRatePerMinute) &&
    Array.isArray(reference.packs) &&
    Array.isArray(reference.plans)
  );
}

/**
 * Resolver of the documentation (`reference`): the base URL of `/v1`, as the API is
 * configured with it, the key format and the price list. Never fails: without an answer the
 * page still renders, its prices withheld, and the proxy keeps it a minute only.
 */
export const resolvePublicApiReference: ResolveFn<PublicApiReference | null> = async () => {
  const http = inject(HttpClient);
  const apiOrigin = inject(API_BASE_URL);
  inject(PageResponse).cache('transient');
  try {
    const answer = await firstValueFrom(getPublicApiReference(http, apiOrigin));
    return isReference(answer.body) ? answer.body : null;
  } catch {
    return null;
  }
};
