import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { PrerenderEpochVolume } from '../../../../../../core/api/generated/models/prerender-epoch-volume';
import type { PrerenderStorageView } from '../../../../../../core/api/generated/models/prerender-storage-view';
import { LOCALES } from '../../../../../../core/i18n/locales';
import { FigurePipe } from '../../../../format/figure-pipe';
import { AdminCard } from '../../../../layout/admin-card';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { Badge, type Tone } from '../../../../widgets/badge';
import { foldRows } from '../../../../widgets/fold-rows';
import { FoldToggle } from '../../../../widgets/fold-toggle';
import { monthlyCost } from '../../backups/monthly-cost';
import { unitPrice } from '../../backups/unit-price';
import { DecimalBytesPipe } from '../format/decimal-bytes-pipe';
import { prerenderTone } from '../prerender-tone';

// About a year of patches shows; the older ones fold under the toggle.
const SHOWN_VERSIONS = 24;
const RETIRED = 'retired';

/**
 * What the page store holds and what keeping it is estimated to cost a month, read from the
 * counters the passes keep (the writer key cannot list): a row per epoch with its state, the
 * bundles of the builds, the total of what is served or kept, then the pages of the served
 * epoch version by version. A line says which prices the estimate takes and what it leaves
 * out: the egress, and how long a retired epoch keeps costing.
 */
@Component({
  selector: 'lodb-storage-section',
  imports: [AdminCard, AdminTextPipe, Badge, DecimalBytesPipe, FigurePipe, FoldToggle],
  templateUrl: './storage-section.html',
  host: { class: 'grid gap-[1.15rem]' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StorageSection {
  readonly storage = input.required<PrerenderStorageView>();

  protected readonly allLocales = LOCALES.length;
  protected readonly versions = foldRows(
    () => this.storage().servedVersions,
    () => SHOWN_VERSIONS,
  );
  protected readonly hasRetired = computed(() =>
    this.storage().epochs.some((epoch) => epoch.state === RETIRED),
  );
  protected readonly prices = computed(() => {
    const storage = this.storage();
    return {
      oneZone: unitPrice(storage.pricePerGbMonthOneZone, storage.currency),
      multiAz: unitPrice(storage.pricePerGbMonthMultiAz, storage.currency),
    };
  });

  protected cost(amount: number): string {
    return monthlyCost(amount, this.storage().currency);
  }

  protected tone(epoch: PrerenderEpochVolume): Tone {
    return prerenderTone(epoch.state);
  }
}
