import { Injectable } from '@angular/core';
import type { PrerenderDemandReport } from '../../../../../../core/api/generated/models/prerender-demand-report';
import type { LivePoll } from '../live/poll/inject-live-poll';
import { injectDemandReport } from './inject-demand-report';

/**
 * The report of the automatic queue, read once for the whole page: the summary card and the
 * « File des 503 » tab show the same answer, and the API is not asked twice a minute.
 * Provided by the page.
 */
@Injectable()
export class DemandReportStore {
  readonly report: LivePoll<PrerenderDemandReport> = injectDemandReport();
}
