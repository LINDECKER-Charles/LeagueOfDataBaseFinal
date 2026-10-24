import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { PrerenderEpochVolume } from '../../../../../../core/api/generated/models/prerender-epoch-volume';
import type { PrerenderReport } from '../../../../../../core/api/generated/models/prerender-report';
import { Button } from '../../../../../../ui/controls/button';
import { figure } from '../../../../format/figure';
import { FigurePipe } from '../../../../format/figure-pipe';
import { AdminCard } from '../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { Badge, type Tone } from '../../../../widgets/badge';
import { monthlyCost } from '../../backups/monthly-cost';
import { unitPrice } from '../../backups/unit-price';
import { ScalewayBillBlock } from '../../scaleway-bill/scaleway-bill-block';
import { type BarSegment, StackedBar } from '../bars/stacked-bar';
import type { LaunchPreset } from '../common/scope/launch-preset';
import { injectAdminDialogs } from '../dialogs/inject-admin-dialogs';
import { DecimalBytesPipe } from '../format/decimal-bytes-pipe';
import { decimalBytes } from '../format/decimal-bytes';
import { entryPreset } from '../overview/blocks/entry-preset';
import type { LaunchLock } from '../overview/state/launch-lock-of';
import { prerenderTone } from '../prerender-tone';
import { epochProjection } from './epoch-projection';
import { RetireDialog } from './switch/retire-dialog';
import { SwitchDialog, type SwitchData } from './switch/switch-dialog';

/** What a row of the epochs offers to do. */
interface EpochActions {
  readonly canSwitch: boolean;
  readonly canComplete: boolean;
  readonly canRetire: boolean;
}

const COSTS = 'prerender.costs';
const STANDBY = 'standby';
const RETIRED = 'retired';

/**
 * The « Stockage et coûts » tab (§ 5.3): the epochs of the store, their state, coverage,
 * pages, volume and monthly cost, and what each offers (prepare the switch to a standby one,
 * complete one with gaps, retire one no longer served), the bundles and the total; the notes
 * on what the estimate counts; what a new epoch would add; then what Scaleway really bills.
 */
@Component({
  selector: 'lodb-costs-tab',
  imports: [
    AdminCard,
    AdminTextPipe,
    Badge,
    Button,
    DecimalBytesPipe,
    FigurePipe,
    ScalewayBillBlock,
    StackedBar,
  ],
  templateUrl: './costs-tab.html',
  host: { class: 'grid gap-[1.15rem]' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CostsTab {
  readonly report = input.required<PrerenderReport>();
  readonly lock = input.required<LaunchLock | null>();
  readonly launch = output<LaunchPreset>();

  private readonly texts = injectAdminText();
  private readonly dialogs = injectAdminDialogs();

  protected readonly storage = computed(() => this.report().storage);
  protected readonly hasRetired = computed(() =>
    this.storage().epochs.some((epoch) => epoch.state === RETIRED),
  );
  // The automatic queue's share of the pages counted in the total: retired epochs left out.
  protected readonly queuePages = computed(() =>
    this.storage()
      .epochs.filter((epoch) => epoch.state !== RETIRED)
      .reduce((sum, epoch) => sum + epoch.queuePages, 0),
  );
  protected readonly prices = computed(() => {
    const storage = this.storage();
    return {
      oneZone: unitPrice(storage.pricePerGbMonthOneZone, storage.currency),
      multiAz: unitPrice(storage.pricePerGbMonthMultiAz, storage.currency),
    };
  });
  protected readonly projection = computed(() => {
    const projection = epochProjection(this.storage());
    return projection
      ? this.texts.text(`${COSTS}.projection`, {
          size: decimalBytes(projection.bytes),
          cost: this.cost(projection.monthlyCost),
        })
      : null;
  });

  protected cost(amount: number): string {
    return monthlyCost(amount, this.storage().currency);
  }

  protected tone(epoch: PrerenderEpochVolume): Tone {
    return prerenderTone(epoch.state);
  }

  protected share(epoch: PrerenderEpochVolume): number | null {
    const coverage = epoch.coverage;
    return coverage && coverage.expectedUnits > 0
      ? coverage.completeUnits / coverage.expectedUnits
      : null;
  }

  protected shareBar(share: number): BarSegment[] {
    const label = figure(share, 'share');
    return [
      { key: 'complete', value: share, color: 'var(--color-good)', label },
      { key: 'missing', value: 1 - share, color: 'var(--color-track)', label },
    ];
  }

  protected actionsOf(epoch: PrerenderEpochVolume): EpochActions {
    const isRetired = epoch.state === RETIRED;
    return {
      canSwitch: epoch.state === STANDBY,
      canComplete: !isRetired && (epoch.coverage?.gapUnits ?? 0) > 0,
      canRetire: isRetired,
    };
  }

  protected complete(epoch: number): void {
    this.launch.emit(entryPreset('complete-epoch', epoch));
  }

  protected prepareSwitch(target: number): void {
    this.dialogs.open<SwitchDialog, SwitchData, void>(SwitchDialog, {
      labelledBy: SwitchDialog.HEADING_ID,
      data: { target, report: this.report() },
    });
  }

  protected retire(target: number): void {
    this.dialogs.open<RetireDialog, SwitchData, void>(RetireDialog, {
      labelledBy: RetireDialog.HEADING_ID,
      data: { target, report: this.report() },
    });
  }
}
