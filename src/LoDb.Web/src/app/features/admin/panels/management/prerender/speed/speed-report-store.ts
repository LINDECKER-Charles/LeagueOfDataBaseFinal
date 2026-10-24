import { Injectable } from '@angular/core';
import type { PrerenderSpeedReport } from '../../../../../../core/api/generated/models/prerender-speed-report';
import type { LivePoll } from '../live/poll/inject-live-poll';
import { injectSpeedReport } from './inject-speed-report';

/**
 * The speed setting and the host's resources, read once for the whole page: the tile of the
 * speed and its block show the same answer, and a setting applied reads it again for both.
 * Provided by the page.
 */
@Injectable()
export class SpeedReportStore {
  readonly report: LivePoll<PrerenderSpeedReport> = injectSpeedReport();
}
