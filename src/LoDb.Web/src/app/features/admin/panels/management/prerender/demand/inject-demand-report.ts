import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import { map } from 'rxjs';
import { API_BASE_URL } from '../../../../../../core/api/api-base-url';
import { readAdminPrerenderDemand } from '../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender-demand';
import type { PrerenderDemandReport } from '../../../../../../core/api/generated/models/prerender-demand-report';
import { PRERENDER_CADENCE } from '../common/scope/prerender-cadence';
import { type LivePoll, injectLivePoll } from '../live/poll/inject-live-poll';

/**
 * The automatic queue, read again every minute, every five while the tab is hidden: it fills
 * and drains by itself, pass or no pass. A failed read keeps the last answer on screen.
 */
export function injectDemandReport(): LivePoll<PrerenderDemandReport> {
  const http = inject(HttpClient);
  const rootUrl = inject(API_BASE_URL);
  const cadence = inject(PRERENDER_CADENCE);
  return injectLivePoll({
    load: () => readAdminPrerenderDemand(http, rootUrl).pipe(map((response) => response.body)),
    every: () => cadence.reportActiveMs,
    hiddenEvery: () => cadence.reportHiddenMs,
  });
}
