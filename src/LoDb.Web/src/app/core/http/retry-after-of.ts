const MS_PER_SECOND = 1000;
const DELAY_SECONDS = /^\d+$/;

/**
 * The delay a `Retry-After` header asks for, in milliseconds: whole seconds, or an HTTP date
 * (already past: no delay). Null without a header or for a value neither form reads.
 */
export function retryAfterOf(header: string | null, now: number): number | null {
  if (header === null) {
    return null;
  }
  const value = header.trim();
  if (DELAY_SECONDS.test(value)) {
    return Number(value) * MS_PER_SECOND;
  }
  const date = Date.parse(value);
  return Number.isNaN(date) ? null : Math.max(0, date - now);
}
