import type { ConnectionHints } from './connection-hints';

/** The connection hints of a window's navigator, if the browser gives any. */
export function connectionOf(view: Window | null): ConnectionHints | undefined {
  const navigator = view?.navigator as (Navigator & { connection?: ConnectionHints }) | undefined;
  return navigator?.connection;
}
