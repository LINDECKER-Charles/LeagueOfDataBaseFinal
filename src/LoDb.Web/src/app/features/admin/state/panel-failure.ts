import { TimeoutError } from 'rxjs';
import { problemOf } from '../shared/http/problem-of';

/**
 * Why a panel could not load:
 * - `timeout`: the API did not answer in time (`ADMIN_TIMEOUT`);
 * - `session`: the session expired or lost its second factor (401, 403);
 * - `network`: the API could not be reached;
 * - `server`: the API answered an error.
 */
export type PanelFailure = 'timeout' | 'session' | 'network' | 'server';

const SESSION_STATUSES: readonly number[] = [401, 403];
// How HttpClient reports a request that got no answer.
const NO_RESPONSE = 0;

/** The failure of a panel whose load threw `error`. */
export function panelFailureOf(error: unknown): PanelFailure {
  if (error instanceof TimeoutError) {
    return 'timeout';
  }
  const { status } = problemOf(error);
  if (SESSION_STATUSES.includes(status)) {
    return 'session';
  }
  return status === NO_RESPONSE ? 'network' : 'server';
}
