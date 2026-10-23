import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import type { PrerenderDemandPage } from '../../../../../../core/api/generated/models/prerender-demand-page';
import { figure } from '../../../../format/figure';
import { stamp } from '../../../../format/stamp';
import { AdminCard } from '../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { Badge } from '../../../../widgets/badge';
import { DemandReportStore } from './demand-report-store';
import { demandStatusOf } from './demand-status-of';

/**
 * The automatic queue of the pages the visitors' SSR refused: whether it runs (read-only,
 * its switch is a setting), what it holds and wrote in the last hour and day, and its ten
 * most requested pending pages. It reads the page's report of the queue
 * (`DemandReportStore`), beside the passes: it needs no launch.
 */
@Component({
  selector: 'lodb-demand-block',
  imports: [AdminCard, AdminTextPipe, Badge],
  templateUrl: './demand-block.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DemandBlock {
  protected readonly report = inject(DemandReportStore).report;
  protected readonly status = computed(() => {
    const report = this.report.value();
    return report ? demandStatusOf(report) : null;
  });

  protected count(value: number): string {
    return figure(value);
  }

  protected seenAt(page: PrerenderDemandPage): string {
    return stamp(page.lastSeenAt, 'minute');
  }
}
