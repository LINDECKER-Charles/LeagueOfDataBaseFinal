import { HttpClient } from '@angular/common/http';
import { effect, inject, untracked } from '@angular/core';
import { map } from 'rxjs';
import { API_BASE_URL } from '../../../../../../../core/api/api-base-url';
import { readAdminPrerenderDemandPages } from '../../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender-demand-pages';
import type { PrerenderDemandPages } from '../../../../../../../core/api/generated/models/prerender-demand-pages';
import { PRERENDER_CADENCE } from '../../common/scope/prerender-cadence';
import { type LivePoll, injectLivePoll } from '../../live/poll/inject-live-poll';

/** Which pages of the queue to read: a state, and a page of its list. */
export interface DemandPagesQuery {
  readonly state: string;
  readonly page: number;
}

/**
 * The pages of the automatic queue in one state, read again every minute, every five while
 * the tab is hidden, like the report of the queue; another state or page reads at once.
 */
export function injectDemandPages(query: () => DemandPagesQuery): LivePoll<PrerenderDemandPages> {
  const http = inject(HttpClient);
  const rootUrl = inject(API_BASE_URL);
  const cadence = inject(PRERENDER_CADENCE);
  const poll = injectLivePoll({
    load: () =>
      readAdminPrerenderDemandPages(http, rootUrl, query()).pipe(map((response) => response.body)),
    every: () => cadence.reportActiveMs,
    hiddenEvery: () => cadence.reportHiddenMs,
  });
  let followed: string | null = null;
  effect(() => {
    const { state, page } = query();
    const key = `${state}:${page}`;
    if (followed !== null && followed !== key) {
      untracked(() => poll.reload());
    }
    followed = key;
  });
  return poll;
}
