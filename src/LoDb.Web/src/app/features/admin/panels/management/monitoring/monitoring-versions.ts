import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { VersionState } from '../../../../../core/api/generated/models/version-state';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { Badge, type Tone } from '../../../widgets/badge';

// Where the ingestion of a version stands, as the API names it, and the tone it reads in.
const STATUS_TONES: Readonly<Record<string, Tone>> = {
  discovered: 'muted',
  ingesting: 'hex',
  ready: 'good',
  failed: 'bad',
};
const COUNTS = ['ready', 'ingesting', 'failed', 'discovered'] as const;

/** The Data Dragon versions of the monitoring: the one served, the counts, the last moves. */
@Component({
  selector: 'lodb-monitoring-versions',
  imports: [Badge, FigurePipe, StampPipe, TranslocoPipe],
  template: `
    @let state = versions();
    <p class="text-sm text-text-muted">
      {{ 'admin.monitoring.versions.current' | transloco }}
      <b class="font-mono text-gold-bright">{{ state.current ?? '—' }}</b>
      @if (state.promotedAt) {
        · {{ state.promotedAt | stamp: 'minute' }}
      }
    </p>
    <p class="mt-3 flex flex-wrap gap-1.5">
      @for (status of counts; track status) {
        <span [lodbBadge]="toneOf(status)">
          {{ texts.term('monitoring.versions.statuses', status) }} · {{ state[status] | figure }}
        </span>
      }
    </p>
    <div class="hx-table-scroll mt-4">
      <table class="hx-table hx-table--flush">
        <thead>
          <tr>
            <th scope="col">{{ 'admin.monitoring.versions.version' | transloco }}</th>
            <th scope="col">{{ 'admin.monitoring.versions.status' | transloco }}</th>
            <th scope="col" class="num">{{ 'admin.monitoring.versions.attempts' | transloco }}</th>
            <th scope="col">{{ 'admin.monitoring.versions.updated' | transloco }}</th>
          </tr>
        </thead>
        <tbody>
          @for (row of state.recent; track row.version) {
            <tr>
              <td class="font-mono text-xs">{{ row.version }}</td>
              <td>
                <span [lodbBadge]="toneOf(row.status)">
                  {{ texts.term('monitoring.versions.statuses', row.status) }}
                </span>
              </td>
              <td class="num">{{ row.attempts | figure }}</td>
              <td class="font-mono text-xs">{{ row.updatedAt | stamp: 'minute' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="4" class="text-text-dim">{{ 'admin.common.no_data' | transloco }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MonitoringVersions {
  readonly versions = input.required<VersionState>();

  protected readonly texts = injectAdminText();
  protected readonly counts = COUNTS;

  protected toneOf(status: string): Tone {
    return STATUS_TONES[status] ?? 'muted';
  }
}
