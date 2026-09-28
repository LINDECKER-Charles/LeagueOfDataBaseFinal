import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { readAdminStorage } from '../../../../../core/api/generated/fn/admin-storage/read-admin-storage';
import { Chip } from '../../../../../ui/controls/chip';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminBand } from '../../../layout/admin-band';
import { AdminCard } from '../../../layout/admin-card';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { Badge, type Tone } from '../../../widgets/badge';
import { Kpi } from '../../../widgets/kpi';
import { RankList } from '../../../widgets/rank-list';

// The WebP coverage the legacy health badge read as good, then as one to watch.
const WEBP_GOOD = 0.95;
const WEBP_WARN = 0.6;

/**
 * The storage of the overview, the legacy `overview-storage` panel: its weight, its versions
 * and its WebP coverage, its families by weight, what the deduplication saves and three
 * badges of health. A storage that could not be read says so in a red band, in place of the
 * figures it would have zeroed.
 */
@Component({
  selector: 'lodb-overview-storage',
  imports: [
    AdminBand,
    AdminCard,
    Badge,
    Chip,
    FigurePipe,
    Kpi,
    PanelState,
    RankList,
    RouterLink,
    AdminTextPipe,
  ],
  templateUrl: './overview-storage.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverviewStorage {
  protected readonly storage = injectPanel(readAdminStorage, () => ({}));
  protected readonly families = computed(() =>
    (this.storage.value()?.families ?? []).map((row) => ({ name: row.name, value: row.bytes })),
  );

  protected webpTone(coverage: number): Tone {
    if (coverage >= WEBP_GOOD) {
      return 'good';
    }
    return coverage >= WEBP_WARN ? 'warn' : 'bad';
  }
}
