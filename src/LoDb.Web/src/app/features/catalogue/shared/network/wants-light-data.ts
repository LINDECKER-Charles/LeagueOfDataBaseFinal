import type { ConnectionHints } from './connection-hints';

// Below 4g, a clip or a list read ahead would take the bandwidth the page itself needs.
const SLOW_CONNECTIONS: readonly string[] = ['slow-2g', '2g', '3g'];

/**
 * Whether the page should fetch only what the reader asks for: they turned Save-Data on, or
 * their connection is slow. Without hints (most browsers), it fetches as usual.
 */
export function wantsLightData(connection: ConnectionHints | undefined): boolean {
  if (connection?.saveData === true) {
    return true;
  }
  return SLOW_CONNECTIONS.includes(connection?.effectiveType ?? '');
}
