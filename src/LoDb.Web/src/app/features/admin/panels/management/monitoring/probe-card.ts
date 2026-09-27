import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { ServiceProbe } from '../../../../../core/api/generated/models/service-probe';
import { Chip } from '../../../../../ui/controls/chip';
import { Frame } from '../../../../../ui/surfaces/frame';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { healthTone } from '../../../shared/health-tone';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { Badge } from '../../../widgets/badge';

/**
 * A probe of the monitoring, the legacy `.panel` card: the service under its human name and
 * its health, then chips of what it measured (latency, version, size of the database, objects
 * of the storage), and why it is not healthy, when it says.
 */
@Component({
  selector: 'lodb-probe-card',
  imports: [Badge, Chip, FigurePipe, AdminTextPipe],
  hostDirectives: [Frame],
  template: `
    @let service = probe();
    <div class="flex flex-wrap items-center justify-between gap-[0.6rem]">
      <h2 class="font-beaufort text-[0.95rem] font-bold tracking-[0.06em] text-gold-bright">
        {{ name() }}
      </h2>
      <span [lodbBadge]="tone()">{{ texts.term('health', service.status) }}</span>
    </div>
    <div class="mt-[0.7rem] flex flex-wrap items-center gap-[0.45rem]">
      <span lodbChip>{{ service.latencyMs }} ms</span>
      @if (service.version) {
        <span lodbChip>{{ service.version }}</span>
      }
      @if (service.databaseBytes) {
        <span lodbChip>
          {{
            'admin.monitoring.probe.database'
              | adminText: { size: (service.databaseBytes | figure: 'bytes') }
          }}
        </span>
      }
      @if (service.objects) {
        <span lodbChip>
          {{
            'admin.monitoring.probe.objects'
              | adminText
                : {
                    count: (service.objects | figure),
                    size: (service.bytes ?? 0 | figure: 'bytes'),
                  }
          }}
        </span>
      }
    </div>
    @if (service.detail) {
      <p class="mt-[0.6rem] text-[0.8rem] text-text-dim">{{ service.detail }}</p>
    }
  `,
  host: { class: 'block px-[1.35rem] py-5' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProbeCard {
  readonly probe = input.required<ServiceProbe>();

  protected readonly texts = injectAdminText();
  protected readonly name = computed(() =>
    this.texts.term('monitoring.services.names', this.probe().name),
  );
  protected readonly tone = computed(() => healthTone(this.probe().status));
}
