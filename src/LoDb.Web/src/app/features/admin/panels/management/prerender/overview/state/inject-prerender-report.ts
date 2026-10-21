import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import { map } from 'rxjs';
import { API_BASE_URL } from '../../../../../../../core/api/api-base-url';
import { readAdminPrerender } from '../../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { PRERENDER_CADENCE } from '../../common/scope/prerender-cadence';
import { type LivePoll, injectLivePoll } from '../../live/poll/inject-live-poll';

/**
 * The report of the pre-rendering, read again and again (§ 2.5): every minute while a pass
 * is active, every two otherwise (a promotion may open one meanwhile), every five while the
 * tab is hidden so that its title and the end of a pass stay true. A failed read keeps the
 * last report on screen: a 502 while the API redeploys must not blank a pass of several days.
 */
export function injectPrerenderReport(): LivePoll<PrerenderReport> {
  const http = inject(HttpClient);
  const rootUrl = inject(API_BASE_URL);
  const cadence = inject(PRERENDER_CADENCE);
  const report: LivePoll<PrerenderReport> = injectLivePoll({
    load: () => readAdminPrerender(http, rootUrl).pipe(map((response) => response.body)),
    every: () => (report.value()?.current ? cadence.reportActiveMs : cadence.reportIdleMs),
    hiddenEvery: () => cadence.reportHiddenMs,
  });
  return report;
}
