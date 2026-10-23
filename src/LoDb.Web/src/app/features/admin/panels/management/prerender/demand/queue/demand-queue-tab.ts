import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Button } from '../../../../../../../ui/controls/button';
import { AdminCard } from '../../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { DemandReportStore } from '../demand-report-store';
import { demandEpochLine } from './demand-epoch-line';
import { DemandHoursChart } from './hours/demand-hours-chart';
import { DemandPageList } from './demand-page-list';
import { type DemandState, demandStateChips } from './demand-state-chips';
import { injectDemandPages } from './inject-demand-pages';

// What the queue wrote is what the operator comes for.
const FIRST_STATE: DemandState = 'done';

/**
 * The « File des 503 » tab: what the automatic queue holds in the served epoch, its last 24
 * hours, then its pages state by state (the chips filter them, written ones first), a page of
 * fifty at a time, read again every minute.
 */
@Component({
  selector: 'lodb-demand-queue-tab',
  imports: [AdminCard, AdminTextPipe, Button, DemandHoursChart, DemandPageList],
  templateUrl: './demand-queue-tab.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DemandQueueTab {
  private readonly texts = injectAdminText();

  protected readonly report = inject(DemandReportStore).report;
  protected readonly state = signal<DemandState>(FIRST_STATE);
  protected readonly page = signal(1);
  private readonly pages = injectDemandPages(() => ({ state: this.state(), page: this.page() }));

  protected readonly chips = computed(() => {
    const report = this.report.value();
    return report ? demandStateChips(report) : [];
  });
  // An answer for another state or page, still in flight when the operator moved, is not shown.
  protected readonly listing = computed(() => {
    const listing = this.pages.value();
    return listing?.state === this.state() && listing.page === this.page() ? listing : null;
  });
  protected readonly pageCount = computed(() => {
    const listing = this.listing();
    return listing ? Math.ceil(listing.total / listing.pageSize) : 0;
  });
  protected readonly epochLine = computed(() => {
    const report = this.report.value();
    return report ? demandEpochLine(report, this.texts) : '';
  });

  protected choose(state: DemandState): void {
    this.state.set(state);
    this.page.set(1);
  }

  protected turn(step: number): void {
    this.page.update((page) => Math.min(Math.max(1, page + step), this.pageCount()));
  }
}
