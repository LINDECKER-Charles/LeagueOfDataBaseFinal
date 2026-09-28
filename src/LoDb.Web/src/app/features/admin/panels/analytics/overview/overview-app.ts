import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { readAdminMonitoring } from '../../../../../core/api/generated/fn/admin-monitoring/read-admin-monitoring';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminBand } from '../../../layout/admin-band';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { healthTone } from '../../../shared/health-tone';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { Badge } from '../../../widgets/badge';
import { Kpi } from '../../../widgets/kpi';

/**
 * The first row of the overview, the legacy `overview-app` panel: the figures of the
 * application and a tile of the services, one badge per probe in the colour of its health,
 * that opens the monitoring. Counters that could not be read say so in a red band.
 */
@Component({
  selector: 'lodb-overview-app',
  imports: [AdminBand, Badge, FigurePipe, Kpi, PanelState, RouterLink, AdminTextPipe],
  template: `
    <lodb-panel-state [panel]="monitoring" shape="kpi" />
    @if (monitoring.value(); as health) {
      @if (health.counters; as counters) {
        <div class="kpi-grid">
          <lodb-kpi
            [label]="'admin.overview.kpi.users' | adminText"
            [value]="counters.usersTotal | figure"
            [sub]="
              'admin.overview.kpi.users_sub'
                | adminText: { week: counters.usersNewWeek, banned: counters.usersBanned }
            "
          />
          <lodb-kpi
            accent="hex"
            [label]="'admin.overview.kpi.builds' | adminText"
            [value]="counters.buildsTotal | figure"
            [sub]="'admin.overview.kpi.builds_sub' | adminText: { count: counters.buildsPublic }"
          />
          <lodb-kpi
            accent="green"
            [label]="'admin.overview.kpi.donations' | adminText"
            [value]="counters.donationsTotalCents | figure: 'euros'"
            [sub]="
              'admin.overview.kpi.donations_sub' | adminText: { count: counters.donationsCount }
            "
          />
          <lodb-kpi
            accent="blue"
            [label]="'admin.overview.kpi.api_keys' | adminText"
            [value]="counters.apiKeysActive | figure"
            [sub]="
              'admin.overview.kpi.api_keys_sub'
                | adminText: { count: (counters.apiRequestsMonth | figure: 'compact') }
            "
          />
          <a
            lodbKpi
            accent="cyan"
            routerLink="/admin/monitoring"
            [isLink]="true"
            [label]="'admin.overview.kpi.services' | adminText"
            [sub]="'admin.overview.kpi.services_sub' | adminText"
          >
            <span class="mt-[0.55rem] flex flex-wrap gap-[0.3rem]">
              @for (probe of health.services; track probe.name) {
                <span [lodbBadge]="tone(probe.status)">{{ probe.name }}</span>
              }
            </span>
          </a>
        </div>
      } @else {
        <lodb-admin-band tone="alert">
          {{ 'admin.overview.counters_unavailable' | adminText }}
        </lodb-admin-band>
      }
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverviewApp {
  protected readonly monitoring = injectPanel(readAdminMonitoring, () => ({}));
  protected readonly tone = healthTone;
}
