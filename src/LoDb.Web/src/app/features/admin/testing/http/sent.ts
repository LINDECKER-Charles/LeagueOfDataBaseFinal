import type { TestRequest } from '@angular/common/http/testing';
import { ADMIN_API } from '../admin-api';
import type { AdminVisit } from '../admin-visit';

// A page sends its calls a few ticks after it renders, a deferred block once its chunk is
// loaded: a spec looks again every few milliseconds, for this long at most.
const WAIT_MS = 2_000;
const POLL_MS = 5;

function pause(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, POLL_MS));
}

/**
 * The call of the page to `path` of the API (without its query), once sent. It waits
 * without the test bed's stability, which an unanswered request holds back.
 */
export async function sent(
  visit: Pick<AdminVisit, 'http'>,
  path: string,
  method = 'GET',
): Promise<TestRequest> {
  const url = `${ADMIN_API}${path}`;
  const deadline = Date.now() + WAIT_MS;
  for (;;) {
    const found = visit.http.match((request) => request.method === method && request.url === url);
    if (found.length === 1 && found[0]) {
      return found[0];
    }
    if (found.length > 1 || Date.now() > deadline) {
      throw new Error(`Expected one ${method} ${url}, found ${found.length}.`);
    }
    await pause();
  }
}
