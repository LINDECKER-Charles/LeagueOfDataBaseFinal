import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { figure } from '../../../../../format/figure';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { Kpi } from '../../../../../widgets/kpi';
import { monthlyCost } from '../../../backups/monthly-cost';
import { decimalBytes } from '../../format/decimal-bytes';

const KPIS = 'prerender.overview.kpis';
const RETIRED = 'retired';

/**
 * The four key figures of the page (§ 2.1): the epoch nginx serves (and the one ready to
 * replace it), what it covers of the older patches, what the store holds, and its estimated
 * monthly cost, with what the retired epochs still cost until deleted.
 */
@Component({
  selector: 'lodb-prerender-kpis',
  imports: [AdminTextPipe, Kpi],
  template: `
    @let data = report();
    <div class="kpi-grid">
      <lodb-kpi
        accent="hex"
        data-kpi="served"
        [label]="'admin.prerender.overview.kpis.served' | adminText"
        [value]="data.servedEpoch"
        [sub]="servedSub()"
      />
      <lodb-kpi
        accent="good"
        data-kpi="coverage"
        [label]="'admin.prerender.overview.kpis.coverage' | adminText"
        [value]="coverage().value"
        [sub]="coverage().sub"
      />
      <lodb-kpi
        accent="blue"
        data-kpi="stored"
        [label]="'admin.prerender.overview.kpis.stored' | adminText"
        [value]="stored()"
        [sub]="
          'admin.prerender.overview.kpis.stored_sub' | adminText: { pages: data.storage.pages }
        "
      />
      <lodb-kpi
        accent="green"
        data-kpi="cost"
        [label]="'admin.prerender.overview.kpis.cost' | adminText"
        [value]="cost()"
        [sub]="costSub()"
      />
    </div>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrerenderKpis {
  readonly report = input.required<PrerenderReport>();

  private readonly texts = injectAdminText();

  protected readonly servedSub = computed(() => {
    const ready = this.report().readyEpoch;
    return ready === null || ready === undefined
      ? this.texts.text(`${KPIS}.served_sub`)
      : this.texts.text(`${KPIS}.served_ready`, { epoch: ready });
  });
  protected readonly coverage = computed(() => {
    const report = this.report();
    const served = report.storage.epochs.find((epoch) => epoch.epoch === report.servedEpoch);
    const coverage = served?.coverage;
    if (!coverage || coverage.expectedUnits === 0) {
      return { value: '—', sub: coverage ? this.texts.text(`${KPIS}.coverage_none`) : null };
    }
    return {
      value: figure(coverage.completeUnits / coverage.expectedUnits, 'share'),
      sub: this.texts.text(`${KPIS}.coverage_sub`, {
        complete: figure(coverage.completeVersions),
        total: coverage.pinnedVersions,
      }),
    };
  });
  protected readonly stored = computed(() => decimalBytes(this.report().storage.bytes));
  protected readonly cost = computed(() => {
    const { monthlyCost: amount, currency } = this.report().storage;
    return monthlyCost(amount, currency);
  });
  // What the retired epochs still cost until the lifecycle deletes them.
  protected readonly costSub = computed(() => {
    const storage = this.report().storage;
    const retired = storage.epochs.filter((epoch) => epoch.state === RETIRED);
    if (retired.length === 0) {
      return this.texts.text(`${KPIS}.cost_sub`);
    }
    const amount = retired.reduce((sum, epoch) => sum + epoch.monthlyCost, 0);
    return this.texts.text(`${KPIS}.cost_sub_retired`, {
      retired: monthlyCost(amount, storage.currency),
    });
  });
}
