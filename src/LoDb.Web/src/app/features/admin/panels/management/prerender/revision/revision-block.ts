import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { figure } from '../../../../format/figure';
import { stamp } from '../../../../format/stamp';
import { AdminCard } from '../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { Badge } from '../../../../widgets/badge';
import { RevisionReportStore } from './revision-report-store';

/**
 * The queue of the pages of the closed states: whether it runs (read-only: it follows the
 * store, never `AutoEnqueueOnPromotion`), what it holds and wrote lately, what is due and when
 * the next page is, its epochs, and how far its producer went through the closures of the
 * index. It reads the page's report of the queue (`RevisionReportStore`), beside the passes:
 * it needs no launch.
 */
@Component({
  selector: 'lodb-revision-block',
  imports: [AdminCard, AdminTextPipe, Badge],
  templateUrl: './revision-block.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RevisionBlock {
  protected readonly report = inject(RevisionReportStore).report;

  protected count(value: number): string {
    return figure(value);
  }

  protected at(value: string | null | undefined): string {
    return value ? stamp(value, 'minute') : '—';
  }
}
