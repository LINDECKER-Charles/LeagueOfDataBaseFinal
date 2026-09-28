import { TimeoutError } from 'rxjs';
import { problemOf } from '../shared/http/problem-of';

/**
 * Why a panel could not load, and the HTTP status of the answer (0 when none came), which
 * the legacy band printed for an error of the API ("HTTP 500"):
 * - `timeout`: the API did not answer in time (`ADMIN_TIMEOUT`);
 * - `session`: the session expired or lost its second factor (401, 403);
 * - `network`: the API could not be reached;
 * - `server`: the API answered an error.
 */
export interface PanelFailure {
  readonly kind: 'timeout' | 'session' | 'network' | 'server';
  readonly status: number;
}

const SESSION_STATUSES: readonly number[] = [401, 403];
// How HttpClient reports a request that got no answer.
const NO_RESPONSE = 0;

/** The failure of a panel whose load threw `error`. */
export function panelFailureOf(error: unknown): PanelFailure {
  if (error instanceof TimeoutError) {
    return { kind: 'timeout', status: NO_RESPONSE };
  }
  const { status } = problemOf(error);
  if (SESSION_STATUSES.includes(status)) {
    return { kind: 'session', status };
  }
  return { kind: status === NO_RESPONSE ? 'network' : 'server', status };
}
