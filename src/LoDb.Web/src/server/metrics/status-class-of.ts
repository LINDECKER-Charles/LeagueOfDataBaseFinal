import type { ServerResponse } from 'node:http';

const STATUS_PER_CLASS = 100;
// A client gone before the whole answer was written: Caddy logs it as status 0, nginx as 499.
const ABORTED = 'aborted';

/**
 * The `status_class` of a finished response (`2xx`…`5xx`), or `aborted` when its client left
 * before it was written: a bounded label, never the status itself.
 */
export function statusClassOf(response: ServerResponse): string {
  if (!response.writableFinished) {
    return ABORTED;
  }
  return `${Math.trunc(response.statusCode / STATUS_PER_CLASS)}xx`;
}
