import { readPositiveInteger } from '../read-positive-integer';
import type { RenderTimeouts } from './render-timeouts';

type Environment = Readonly<Record<string, string | undefined>>;

const MS_PER_SECOND = 1_000;
// nginx gives up on a render after 30 s (snippets/rendered-pages.conf): a render that stops
// calling the API at 20 s still answers in time, and holds its admission slot no longer.
// One slow call may not take more than half of it, so a page reading /api/meta and then its
// detail can still fail fast, with a 502, rather than wait for the proxy's timeout.
const DEFAULT_DEADLINE_SECONDS = 20;
const DEFAULT_API_CALL_SECONDS = 10;
const DEADLINE_VARIABLE = 'LODB_RENDER_DEADLINE_SECONDS';
const API_CALL_VARIABLE = 'LODB_RENDER_API_TIMEOUT_SECONDS';

/**
 * Reads how long a render may wait on the API from `LODB_RENDER_DEADLINE_SECONDS` and
 * `LODB_RENDER_API_TIMEOUT_SECONDS`, each optional; one call never outlasts the deadline.
 */
export function readRenderTimeouts(env: Environment): RenderTimeouts {
  const deadline = readPositiveInteger(env, DEADLINE_VARIABLE, DEFAULT_DEADLINE_SECONDS);
  const apiCall = readPositiveInteger(env, API_CALL_VARIABLE, DEFAULT_API_CALL_SECONDS);
  if (apiCall > deadline) {
    throw new Error(
      `${API_CALL_VARIABLE} must stay within ${DEADLINE_VARIABLE}, got ${apiCall} for ${deadline}.`,
    );
  }
  return { deadlineMs: deadline * MS_PER_SECOND, apiCallMs: apiCall * MS_PER_SECOND };
}
