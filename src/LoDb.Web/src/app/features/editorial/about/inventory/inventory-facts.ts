import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { InventoryFact } from './inventory-fact';
import type { InventorySnapshot } from './inventory-snapshot';
import { inventoryTiles } from './inventory-tiles';

/** The facts asked for as a grid of tiles, with the snapshot's numbers or `—` without one. */
@Component({
  selector: 'lodb-inventory-facts',
  imports: [TranslocoPipe],
  template: `<dl class="my-6 grid grid-cols-[repeat(auto-fit,minmax(9.5rem,1fr))] gap-3">
    @for (tile of tiles(); track tile.label) {
      <div class="flex flex-col-reverse border border-gold-deep/45 bg-void/70 px-4 py-3.5">
        <dt class="mt-1.5 text-[0.7rem] tracking-[0.14em] text-text-dim uppercase">
          {{ tile.label | transloco }}
        </dt>
        <dd class="font-beaufort text-[1.75rem] leading-[1.1] text-hex-bright">
          {{ tile.value }}
        </dd>
      </div>
    }
  </dl>`,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryFacts {
  readonly facts = input.required<readonly InventoryFact[]>();
  readonly snapshot = input<InventorySnapshot | null>(null);

  protected readonly tiles = computed(() => inventoryTiles(this.facts(), this.snapshot()));
}
