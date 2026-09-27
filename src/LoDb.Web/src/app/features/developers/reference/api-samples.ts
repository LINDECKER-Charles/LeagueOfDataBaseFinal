import type { FreePlanTerms } from '../../../core/api/generated/models/free-plan-terms';

/** The start of every key, the documentation's fallback when the API gave none. */
export const DEFAULT_KEY_PREFIX = 'lodb_';

/** The made-up consumption of the `/v1/usage` sample; quota and rate are the free plan's. */
export const SAMPLE_USED = 137;

const JSON_INDENT = 2;

/**
 * The error envelope every refusal of `/v1` writes, with the one of a rate limit, on one line
 * as the legacy page printed it.
 */
export const ERROR_SAMPLE =
  '{ "error": { "code": "rate_limited", ' +
  '"message": "rate limit exceeded, retry after X-RateLimit-Reset" } }';

// The legacy placeholder of the curl lines, the same in every locale.
const KEY_PLACEHOLDER = 'votrecle…';

/** The two headers a key may travel in. */
export function authHeaders(prefix: string): string {
  return `Authorization: Bearer ${prefix}…\nX-Api-Key: ${prefix}…`;
}

/** Two calls to copy, one per header, against `baseUrl` (no trailing slash). */
export function curlSamples(baseUrl: string, prefix: string): string {
  const key = `${prefix}${KEY_PLACEHOLDER}`;
  return [
    `curl -H "Authorization: Bearer ${key}" \\`,
    `  "${baseUrl}/v1/trends/champions?range=30d"`,
    '',
    `curl -H "X-Api-Key: ${key}" \\`,
    `  "${baseUrl}/v1/usage"`,
  ].join('\n');
}

/**
 * The answer of `/v1/usage` for a free key: the quota and the rate come from the API, so
 * the sample never contradicts the price list of the same page; only the usage is made up.
 */
export function usageSample(free: FreePlanTerms): string {
  const sample = {
    plan: 'free',
    monthly_quota: free.monthlyQuota,
    used_this_month: SAMPLE_USED,
    remaining_this_month: Math.max(0, free.monthlyQuota - SAMPLE_USED),
    credits_balance: 0,
    rate_limit_per_min: free.ratePerMinute,
  };
  return JSON.stringify(sample, null, JSON_INDENT);
}
