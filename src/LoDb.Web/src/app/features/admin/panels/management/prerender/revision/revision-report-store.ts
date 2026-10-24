import { Injectable } from '@angular/core';
import type { PrerenderRevisionReport } from '../../../../../../core/api/generated/models/prerender-revision-report';
import type { LivePoll } from '../live/poll/inject-live-poll';
import { injectRevisionReport } from './inject-revision-report';

/**
 * The report of the queue of the closed states, read once for the whole page: the tile of the
 * automatic queues and the queue's block show the same answer. Provided by the page.
 */
@Injectable()
export class RevisionReportStore {
  readonly report: LivePoll<PrerenderRevisionReport> = injectRevisionReport();
}
