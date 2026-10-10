// The client addresses the bench states in X-Forwarded-For, which nginx believes from the
// bench's network only (compose.bench.yaml). Documentation ranges (RFC 5737): public, so the
// stack counts each as a visitor, and no real client ever has one.
// Plain module: imported by the k6 scripts and by node --test alike.

const CROWD_NETWORK = '203.0.113.';
const FIRST_HOST = 1;
const LARGEST_CROWD = 254;

/** The bench's one human visitor, apart from every crowd. */
export const VISITOR_ADDRESS = '198.51.100.7';

/** `size` distinct client addresses, 203.0.113.1 onwards. */
export function crowdOf(size) {
  if (!Number.isInteger(size) || size < 1 || size > LARGEST_CROWD) {
    throw new RangeError(`A crowd holds 1 to ${LARGEST_CROWD} addresses, not ${size}.`);
  }
  return Array.from({ length: size }, (_, host) => `${CROWD_NETWORK}${host + FIRST_HOST}`);
}

/** The address of the `iteration`-th request: each client of the crowd in turn. */
export function addressAt(crowd, iteration) {
  return crowd[iteration % crowd.length];
}
