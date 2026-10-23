import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { PrerenderDemandListedPage } from '../../../../../../../core/api/generated/models/prerender-demand-listed-page';
import { stamp } from '../../../../../format/stamp';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { Badge } from '../../../../../widgets/badge';
import { DecimalBytesPipe } from '../../format/decimal-bytes-pipe';

/**
 * A page of the list of the queue's pages: each one's path, a link to the page on the site in
 * a new tab, how often visitors were refused it and when last, when the queue wrote it, its
 * class, whether a pass wrote it again since, its failed tries and why it waits.
 */
@Component({
  selector: 'lodb-demand-page-list',
  imports: [AdminTextPipe, Badge, DecimalBytesPipe],
  templateUrl: './demand-page-list.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DemandPageList {
  readonly pages = input.required<readonly PrerenderDemandListedPage[]>();

  protected at(iso: string): string {
    return stamp(iso, 'minute');
  }
}
