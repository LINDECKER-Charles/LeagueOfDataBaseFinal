import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { PrerenderTypeShares } from '../../../../../../../core/api/generated/models/prerender-type-shares';
import type { ResourceType } from '../../../../../../../core/api/generated/models/resource-type';
import { RESOURCE_TYPE } from '../../../../../../../core/api/generated/models/resource-type-array';
import { Icon } from '../../../../../../../ui/media/icon';
import type { IconName } from '../../../../../../../ui/media/icon-name';
import { figure } from '../../../../../format/figure';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';

/** One type of page of the perimeter. */
interface TypeChip {
  readonly type: ResourceType;
  readonly icon: IconName;
  readonly isOn: boolean;
  /** « 58 % », its share of a version's pages; null before any unit completed. */
  readonly share: string | null;
}

const ICONS: Readonly<Record<ResourceType, IconName>> = {
  champions: 'champion',
  items: 'item',
  runes: 'rune',
  summoners: 'spell',
};

/**
 * « Types de pages » (§ 3.4): a toggle per type, with its icon, its name and its share of the
 * pages. One at least. Once one is left out, a golden note says that a pass kept to some
 * types takes no promoted patch: they wait for its end (VersionEnqueuer).
 */
@Component({
  selector: 'lodb-type-chips',
  imports: [AdminTextPipe, Icon],
  template: `
    <section class="mt-6" role="group" aria-labelledby="launch-types-title">
      <div class="mb-2 flex items-center gap-3">
        <h3 id="launch-types-title" class="readout-label">
          {{ 'admin.prerender.launcher.types.title' | adminText }}
        </h3>
        <span class="font-mono text-[0.82rem] text-text-dim">
          {{ selected().length }} / {{ chips().length }}
        </span>
      </div>
      <div class="flex flex-wrap gap-2">
        @for (chip of chips(); track chip.type) {
          <button
            type="button"
            class="inline-flex min-h-11 items-center gap-2 border border-gold/28 px-3 text-[0.88rem] text-text-muted aria-pressed:border-hex aria-pressed:bg-hex/10 aria-pressed:text-gold-bright"
            [attr.aria-pressed]="chip.isOn"
            [attr.data-type]="chip.type"
            (click)="toggle(chip.type)"
          >
            <lodb-icon class="size-4" [name]="chip.icon" />
            {{ 'admin.prerender.page_types.' + chip.type | adminText }}
            @if (chip.share; as share) {
              <span class="font-mono text-[0.75rem] text-text-dim">
                {{ 'admin.prerender.launcher.types.share' | adminText: { share } }}
              </span>
            }
          </button>
        }
      </div>
      @if (selected().length === 0) {
        <p class="mt-2 text-[0.85rem] text-danger-light" data-types-required>
          {{ 'admin.prerender.launcher.types.required' | adminText }}
        </p>
      } @else if (isRestricted() && enqueuesPromotions()) {
        <p class="mt-2 text-[0.85rem] text-gold" data-types-defer>
          {{ 'admin.prerender.launcher.types.defer' | adminText }}
        </p>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TypeChips {
  /** The types chosen. */
  readonly selected = input.required<readonly string[]>();
  /** Each type's share of a version's pages; null before any unit completed. */
  readonly shares = input.required<PrerenderTypeShares | null>();
  /** Whether a patch leaving the latest rank joins the active pass by itself. */
  readonly enqueuesPromotions = input.required<boolean>();
  /** The types chosen once one is toggled, in the API's order. */
  readonly changed = output<readonly string[]>();

  protected readonly chips = computed<TypeChip[]>(() => {
    const shares = this.shares();
    return RESOURCE_TYPE.map((type) => ({
      type,
      icon: ICONS[type],
      isOn: this.selected().includes(type),
      share: shares ? figure(shares[type], 'percent') : null,
    }));
  });
  protected readonly isRestricted = computed(() => this.selected().length < RESOURCE_TYPE.length);

  protected toggle(type: ResourceType): void {
    const chosen = new Set(this.selected());
    if (!chosen.delete(type)) {
      chosen.add(type);
    }
    this.changed.emit(RESOURCE_TYPE.filter((each) => chosen.has(each)));
  }
}
