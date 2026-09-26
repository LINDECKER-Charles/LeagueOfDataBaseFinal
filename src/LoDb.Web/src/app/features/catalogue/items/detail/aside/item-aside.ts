import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ItemDetails } from '../../../../../core/api/generated/models/item-details';
import type { PageContext } from '../../../../../core/context/page-context';
import { Frame } from '../../../../../ui/surfaces/frame';
import { injectCatalogueLink } from '../../codex/links/inject-catalogue-link';
import { tagLabelOf } from '../../list/facets/tag-label-of';
import { formatItemStat } from '../../stats/format-item-stat';
import { mapLabelKey } from './map-label-key';

/**
 * The aside of an item page: its stat block, its gold ledger, where and for whom it is
 * available, and its categories. A panel with nothing to say is left out.
 */
@Component({
  selector: 'lodb-item-aside',
  imports: [Frame, RouterLink, TranslocoPipe],
  templateUrl: './item-aside.html',
  styleUrl: './item-aside.css',
  host: { class: 'mt-12 block space-y-6 lg:sticky lg:top-6 lg:mt-0' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ItemAside {
  readonly details = input.required<ItemDetails>();
  readonly context = input.required<PageContext>();

  protected readonly linkOf = injectCatalogueLink();
  protected readonly tagLabel = tagLabelOf;
  protected readonly mapLabel = mapLabelKey;

  protected readonly stats = computed(() =>
    this.details().profile.stats.map((row) => ({
      label: `stat.${row.stat}`,
      value: formatItemStat(row),
    })),
  );
  /** Only an epic or a legendary item states its tier, as the legacy depth did. */
  protected readonly tier = computed(() => {
    const tier = this.details().profile.tier;
    return tier === 'epic' || tier === 'legendary' ? tier : null;
  });
  protected readonly bindings = computed(() => {
    const { requiredChampion, requiredAlly } = this.details();
    return [requiredChampion, requiredAlly].filter((link) => !!link);
  });
  protected readonly hasAvailability = computed(
    () =>
      this.tier() !== null || this.bindings().length > 0 || this.details().availableMaps.length > 0,
  );
}
