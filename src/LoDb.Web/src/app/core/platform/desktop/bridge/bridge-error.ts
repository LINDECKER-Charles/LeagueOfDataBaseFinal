/**
 * A request of the bridge that failed: the host's `error` code (`invalid-url`, `busy`,
 * `no-update`…), or one of the page's own: `unavailable` without a bridge, `timeout` when
 * no reply came in time.
 */
export class BridgeError extends Error {
  constructor(readonly code: string) {
    super(`The desktop host refused the request: ${code}.`);
    this.name = 'BridgeError';
  }
}
