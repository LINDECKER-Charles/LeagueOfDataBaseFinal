import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import { map } from 'rxjs';
import { API_BASE_URL } from '../../../../../../core/api/api-base-url';
import { readAdminPrerenderRevision } from '../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender-revision';
import type { PrerenderRevisionReport } from '../../../../../../core/api/generated/models/prerender-revision-report';
import { PRERENDER_CADENCE } from '../common/scope/prerender-cadence';
import { type LivePoll, injectLivePoll } from '../live/poll/inject-live-poll';

/**
 * The queue of the closed states, read again every minute, every five while the tab is hidden:
 * each promotion fills it and it drains by itself. A failed read keeps the last answer on
 * screen.
 */
export function injectRevisionReport(): LivePoll<PrerenderRevisionReport> {
  const http = inject(HttpClient);
  const rootUrl = inject(API_BASE_URL);
  const cadence = inject(PRERENDER_CADENCE);
  return injectLivePoll({
    load: () => readAdminPrerenderRevision(http, rootUrl).pipe(map((response) => response.body)),
    every: () => cadence.reportActiveMs,
    hiddenEvery: () => cadence.reportHiddenMs,
  });
}
