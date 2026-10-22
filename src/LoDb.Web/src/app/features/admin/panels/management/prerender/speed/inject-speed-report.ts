import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import { map } from 'rxjs';
import { API_BASE_URL } from '../../../../../../core/api/api-base-url';
import { readAdminPrerenderSpeed } from '../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender-speed';
import type { PrerenderSpeedReport } from '../../../../../../core/api/generated/models/prerender-speed-report';
import { type LivePoll, injectLivePoll } from '../live/poll/inject-live-poll';

// The API reads the host every 30 s: reading it more often shows nothing new.
const EVERY_MS = 30_000;
const HIDDEN_EVERY_MS = 300_000;

/**
 * The speed setting and the host's resources, read every 30 s while the page shows, every five
 * minutes while it is hidden; a failed read keeps the last one on screen.
 */
export function injectSpeedReport(): LivePoll<PrerenderSpeedReport> {
  const http = inject(HttpClient);
  const rootUrl = inject(API_BASE_URL);
  return injectLivePoll({
    load: () => readAdminPrerenderSpeed(http, rootUrl).pipe(map((response) => response.body)),
    every: () => EVERY_MS,
    hiddenEvery: () => HIDDEN_EVERY_MS,
  });
}
