import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { MonitoringReport } from '../../../../../core/api/generated/models/monitoring-report';
import { duration } from '../../../format/duration';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { Kpi } from '../../../widgets/kpi';
import { MonitoringVersions } from './monitoring-versions';

/**
 * What the new API reports beyond the legacy monitoring, after its sections: the figures of
 * its process, the queues of the ingestion and the Data Dragon versions.
 */
@Component({
  selector: 'lodb-monitoring-runtime',
  imports: [AdminCard, AdminRule, FigurePipe, Kpi, MonitoringVersions, AdminTextPipe],
  template: `
    @let data = report();
    <lodb-admin-rule [label]="'admin.monitoring.process.title' | adminText" />
    <div class="kpi-grid">
      <lodb-kpi
        [label]="'admin.monitoring.process.version' | adminText"
        [value]="data.process.version"
        [sub]="data.process.revision ?? null"
      />
      <lodb-kpi
        accent="hex"
        [label]="'admin.monitoring.process.uptime' | adminText"
        [value]="uptime()"
      />
      <lodb-kpi
        accent="blue"
        [label]="'admin.monitoring.process.working_set' | adminText"
        [value]="data.process.workingSetBytes | figure: 'bytes'"
        [sub]="
          'admin.monitoring.process.heap'
            | adminText: { size: (data.process.managedHeapBytes | figure: 'bytes') }
        "
      />
      <lodb-kpi
        accent="green"
        [label]="'admin.monitoring.process.threads' | adminText"
        [value]="data.process.threadPoolThreads | figure"
        [sub]="
          'admin.monitoring.process.pending' | adminText: { count: data.process.pendingWorkItems }
        "
      />
      <lodb-kpi
        accent="red"
        [label]="'admin.monitoring.process.cpu' | adminText"
        [value]="data.process.cpuSeconds | figure"
      />
      <lodb-kpi
        accent="cyan"
        [label]="'admin.monitoring.process.collections' | adminText"
        [value]="data.process.collections.join(' / ')"
      />
    </div>

    <lodb-admin-rule [label]="'admin.monitoring.ingestion.title' | adminText" />
    <div class="kpi-grid">
      <lodb-kpi
        [label]="'admin.monitoring.ingestion.versions' | adminText"
        [value]="data.ingestion.versionBacklog | figure"
      />
      <lodb-kpi
        accent="hex"
        [label]="'admin.monitoring.ingestion.on_demand' | adminText"
        [value]="data.ingestion.onDemandBacklog | figure"
      />
    </div>
    @if (data.versions; as versions) {
      <lodb-admin-card
        class="mt-[1.15rem]"
        [heading]="'admin.monitoring.versions.title' | adminText"
      >
        <lodb-monitoring-versions [versions]="versions" />
      </lodb-admin-card>
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MonitoringRuntime {
  readonly report = input.required<MonitoringReport>();

  protected readonly uptime = computed(() => duration(this.report().process.uptimeSeconds));
}
